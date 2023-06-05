using DocuSign.eSign.Api;
using DocuSign.eSign.Client;
using System;
using Terrasoft.Core.DB;
using Terrasoft.Core;
using DocuSign.eSign.Model;
using System.Data;
using System.Linq;
using Terrosoft.Configuration;
using Terrasoft.Core.Factories;

namespace Avt.DocuSignLib.Files.cs
{
    [DefaultBinding(typeof(ITemplateDocuSignHelper))]
    public class TemplateDocuSignHelper : ITemplateDocuSignHelper
    {
        private UserConnection userConnection;
        private UserConnection logUserConnection;

        public TemplateDocuSignHelper(UserConnection uc)
        {
            logUserConnection = uc;
            userConnection = uc.AppConnection.SystemUserConnection;
        }

        public string UpdateTemplate(string userId, string clientId, string keyString)
        {
            var helper = new GetTokenDocuSign();
            var logHelper = new LogHelper();
            try
            {
                var auth = helper.GetOAuthToken(userId, clientId, keyString);
                var token = auth.access_token;
                var account = helper.GetAccountDocuSign(auth);

                var docuSignClient = new DocuSignClient(account.BaseUri + "/restapi");
                docuSignClient.Configuration.DefaultHeader.Add("Authorization", "Bearer " + token);

                TemplatesApi templateApi = new TemplatesApi(docuSignClient);
                var templates = templateApi.ListTemplates(account.AccountId);
                var newTemplate = 0;
                var updateTemplate = 0;
                var newRecipient = 0;
                var updateRecipient = 0;
                var delTemplate = 0;
                var delRecipient = 0;

                if (templates == null || templates.EnvelopeTemplates.Count == 0)
                {
                    logHelper.InsertLog("Templates are not exist", logUserConnection);
                    throw new Exception("Templates are not exist");
                }
                foreach (var template in templates.EnvelopeTemplates)
                {
                    var id = template.TemplateId;
                    var name = template.Name;
                    var select = new Select(userConnection)
                        .Column("Name")
                        .From("AvtDocuSignTemplate")
                        .Where("AvtDocuSignTemplateId").IsEqual(Column.Parameter(id))
                    as Select;
                    var oldName = select.ExecuteScalar<string>();
                    if (oldName == string.Empty)
                    {
                        var insert = new Insert(userConnection).Into("AvtDocuSignTemplate")
                            .Set("AvtDocuSignTemplateId", Column.Parameter(id))
                            .Set("AvtDocuSignAccountId", Column.Parameter(account.AccountId))
                            .Set("Name", Column.Parameter(name));
                        insert.Execute();
                        var recipients = templateApi.ListRecipients(account.AccountId, id);
                        var templateId = GetTemplateId(id);
                        foreach (var recipient in recipients.Signers)
                        {
                            UpdateRecipient(recipient, templateId);
                            newRecipient++;
                        }
                        newTemplate++;

                    }
                    else if (oldName != name)
                    {
                        var update = new Update(userConnection, "AvtDocuSignTemplate")
                            .Set("Name", Column.Parameter(name))
                            .Where("AvtDocuSignTemplateId").IsEqual(Column.Parameter(id)) as Update;
                        update.Execute();
                        updateTemplate++;
                        var recipients = templateApi.ListRecipients(account.AccountId, id);
                        var templateId = GetTemplateId(id);
                        foreach (var recipient in recipients.Signers)
                        {
                            var action = UpdateRecipient(recipient, templateId);
                            if (action == "update") updateRecipient++;
                            if (action == "insert") newRecipient++;
                        }
                        delRecipient += DeleteRecipients(recipients.Signers.Select(x => x.RecipientIdGuid).ToArray(), templateId);
                    }
                    else
                    {
                        var recipients = templateApi.ListRecipients(account.AccountId, id);
                        var templateId = GetTemplateId(id);
                        foreach (var recipient in recipients.Signers)
                        {
                            var action = UpdateRecipient(recipient, templateId);
                            if (action == "update") updateRecipient++;
                            if (action == "insert") newRecipient++;
                        }
                        delRecipient += DeleteRecipients(recipients.Signers.Select(x => x.RecipientIdGuid).ToArray(), templateId);
                    }
                }

                var dels = DeleteTemplatesAndRecipients(templates.EnvelopeTemplates.Select(x => x.TemplateId).ToArray(), account.AccountId);
                delRecipient += dels.Item1;
                delTemplate = dels.Item2;

                var log = $"New templates: {newTemplate}, update templates: {updateTemplate}, delete templates: {delTemplate}, new recipients: {newRecipient}, update recipients: {updateRecipient}, delete recipients: {delRecipient}";

                logHelper.InsertLog(log, logUserConnection);

                return log;
            }
            catch (Exception ex)
            {
                logHelper.InsertLog(ex.Message, logUserConnection);
                return ex.Message;
            }
            
        }

        private string UpdateRecipient(Signer signer, Guid templateId)
        {
            var select = new Select(userConnection)
                .Top(1)
                .Column("AvtRoleName")
                .Column("AvtPosition")
                .From("AvtDocuSignTemplateRecipient")
                .Where("AvtTemplateId").IsEqual(Column.Const(templateId))
                .And("AvtRecipientId").IsEqual(Column.Parameter(signer.RecipientIdGuid))
            as Select;

            var oldName = string.Empty;
            var oldPosition = 0;

            using (DBExecutor executor = userConnection.EnsureDBConnection())
            {
                using (IDataReader reader = select.ExecuteReader(executor))
                {
                    int roleNameColumnIndex = reader.GetOrdinal("AvtRoleName");
                    int positionColumnIndex = reader.GetOrdinal("AvtPosition");
                    while (reader.Read())
                    {
                        oldName = reader.GetValue(roleNameColumnIndex).ToString();
                        oldPosition = Int32.Parse(reader.GetValue(positionColumnIndex).ToString());
                    }
                }
            }
            if (oldName == string.Empty)
            {
                var insert = new Insert(userConnection).Into("AvtDocuSignTemplateRecipient")
                    .Set("AvtRecipientId", Column.Parameter(signer.RecipientIdGuid))
                    .Set("AvtTemplateId", Column.Parameter(templateId))
                    .Set("AvtPosition", Column.Parameter(Int32.Parse(signer.RoutingOrder)))
                    .Set("AvtRoleName", Column.Parameter(signer.RoleName));
                insert.Execute();
                return "insert";

            }
            else if (oldName != signer.RoleName || oldPosition.ToString() != signer.RoutingOrder)
            {
                var update = new Update(userConnection, "AvtDocuSignTemplateRecipient")
                    .Set("AvtRoleName", Column.Parameter(signer.RoleName))
                    .Set("AvtPosition", Column.Parameter(Int32.Parse(signer.RoutingOrder)))
                    .Where("AvtTemplateId").IsEqual(Column.Const(templateId))
                    .And("AvtRecipientId").IsEqual(Column.Parameter(signer.RecipientIdGuid))
                as Update;
                update.Execute();
                return "update";
            }
            else
            {
                return string.Empty;
            }
        }

        private Guid GetTemplateId(string tempalteId)
        {
            var select = new Select(userConnection)
                    .Column("Id")
                    .From("AvtDocuSignTemplate")
                    .Where("AvtDocuSignTemplateId").IsEqual(Column.Parameter(tempalteId))
                as Select;
            return select.ExecuteScalar<Guid>();
        }

        private (int, int) DeleteTemplatesAndRecipients(string[] templatesIds, string accountId)
        {
            var delete = new Delete(userConnection)
                .From("AvtDocuSignTemplateRecipient")
                .Where("AvtTemplateId").Not().In(
                    new Select(userConnection)
                        .Column("Id")
                        .From("AvtDocuSignTemplate")
                        .Where("AvtDocuSignTemplateId").In(Column.Parameters(templatesIds.ToList()))
                        as Select
                )
                .And("AvtTemplateId").In(
                    new Select(userConnection)
                        .Column("dst", "Id")
                        .From("AvtDocuSignTemplate").As("dst")
                        .Where("dst", "AvtDocuSignAccountId").IsEqual(Column.Parameter(accountId)) as Select
                );
            var sql = delete.GetSqlText();
            var recipient = delete.Execute();

            delete = new Delete(userConnection)
                .From("AvtDocuSignTemplate")
                .Where("AvtDocuSignTemplateId").Not().In(Column.Parameters(templatesIds.ToList()))
                .And("AvtDocuSignAccountId").IsEqual(Column.Parameter(accountId));
            var template = delete.Execute();
            return (recipient, template);

        }

        private int DeleteRecipients(string[] recipientIds, Guid templateId)
        {
            var delete = new Delete(userConnection)
                 .From("AvtDocuSignTemplateRecipient")
                 .Where("AvtRecipientId").Not().In(Column.Parameters(recipientIds.ToList()))
                 .And("AvtTemplateId").IsEqual(Column.Const(templateId));
            return delete.Execute();
        }
    }
}
