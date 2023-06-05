using DocuSign.eSign.Client;
using static DocuSign.eSign.Client.Auth.OAuth;
using System;
using System.Linq;
using System.ServiceModel;
using Terrasoft.Web.Common;
using Terrasoft.Core;
using System.ServiceModel.Activation;
using Terrasoft.Web.Common.ServiceRouting;
using System.ServiceModel.Web;
using System.Collections.Generic;
using DocuSign.eSign.Model;
using UserInfo = DocuSign.eSign.Client.Auth.OAuth.UserInfo;
using DocuSign.eSign.Api;
using Terrasoft.Core.DB;
using Newtonsoft.Json;

namespace Avt.DocuSignLib.Files.cs
{
    [ServiceContract]
    [DefaultServiceRoute]
    [SspServiceRoute]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Required)]
    public class AvtDocuSignService : BaseService
    {

        public AvtDocuSignService() : base()
        {

        }

        public AvtDocuSignService(UserConnection userConnection) : base(userConnection)
        {

        }

        [OperationContract]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.Wrapped,
          RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        public ResponseDocuSign Auth(string userId, string clientId, string key)
        {
            try
            {
                var helper = new GetTokenDocuSign();
                var auth = helper.GetOAuthToken(userId, clientId, key);
                var token = auth.access_token;
                var docuSignClient = new DocuSignClient();
                docuSignClient.SetOAuthBasePath("account-d.docusign.com");
                UserInfo userInfo = docuSignClient.GetUserInfo(token);
                var acct = userInfo.Accounts.FirstOrDefault();

                return new ResponseDocuSign
                {
                    Success = true,
                    Message = $"Connection is true, account name -  {acct.AccountName}"
                };
            } catch (Exception ex)
            {
                return new ResponseDocuSign
                {
                    Success = false,
                    Message = ex.Message
                };
            }

        }

        [OperationContract]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.Wrapped,
          RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        public ResponseDocuSign UpdateTemplate(string userId, string clientId, string key)
        {
            try
            {
                var helper = new TemplateDocuSignHelper(UserConnection);
                var templates = helper.UpdateTemplate(userId, clientId, key);
                return new ResponseDocuSign
                {
                    Success = true,
                    Message = templates
                };
            }
            catch (Exception ex)
            {
                return new ResponseDocuSign
                {
                    Success = false,
                    Message = ex.Message
                };
            }

        }

        //[OperationContract]
        //[WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.Wrapped,
        //  RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        //public string SendSign(Guid idDocument)
        //{
        //    var auth = GetOAuthToken();
        //    var docuSignClient = new DocuSignClient();
        //    docuSignClient.SetOAuthBasePath("account-d.docusign.com");
        //    UserInfo userInfo = docuSignClient.GetUserInfo(auth.access_token);
        //    var acct = userInfo.Accounts.FirstOrDefault();
        //    Byte[] document = GetDocument(idDocument);
        //    var env = GetEnv(auth, document, acct.BaseUri + "/restapi", auth.access_token, acct.AccountId);
        //    return env;
        //}

        private string GetEnv(OAuthToken auth, Byte[] document, string url, string accessToken, string accountId)
        {
            
            EnvelopeDefinition env = new EnvelopeDefinition();
            env.EmailSubject = "Please sign this document set";
            string docString = Convert.ToBase64String(document);
            Document doc = new Document
            {
                DocumentBase64 = docString,
                Name = "Battle Plan",
                FileExtension = "docx",
                DocumentId = "1",
            };

            env.Documents = new List<Document> { doc };

            Signer signer1 = new Signer
            {
                Email = "georgij999@mail.ru",
                Name = "Yegor",
                RecipientId = "1",
                RoutingOrder = "1",
            };
            SignHere signHere1 = new SignHere
            {
                AnchorString = "**signature_1**",
                AnchorUnits = "pixels",
                AnchorYOffset = "10",
                AnchorXOffset = "20",
            };
            Tabs signer1Tabs = new Tabs
            {
                SignHereTabs = new List<SignHere> { signHere1 },
            };
            signer1.Tabs = signer1Tabs;

            Recipients recipients = new Recipients
            {
                Signers = new List<Signer> { signer1 }
            };
            env.Recipients = recipients;


            env.Status = "sent";

            var docuSignClient = new DocuSignClient(url);
            docuSignClient.Configuration.DefaultHeader.Add("Authorization", "Bearer " + accessToken);
            EnvelopesApi envelopesApi = new EnvelopesApi(docuSignClient);
            EnvelopeSummary results = envelopesApi.CreateEnvelope(accountId, env);
            var m = envelopesApi.GetEnvelope(accountId, "6d81ac80-1804-4a90-a73a-9359ef854a40");
            var js = JsonConvert.SerializeObject(m);
            Recipients recips = envelopesApi.ListRecipients(accountId, "6d81ac80-1804-4a90-a73a-9359ef854a40");
            var rec = JsonConvert.SerializeObject(recips);


            TemplatesApi etr = new TemplatesApi(docuSignClient);
            var temp = etr.ListTemplates(accountId);
            return results.EnvelopeId;
        }

        private Byte[] GetDocument(Guid idDocument)
        {
            var select = new Select(UserConnection)
                .Column("Data")
                .From("AvtDocuFile")
                .Where("Id").IsEqual(Column.Const(idDocument))
            as Select;

            return select.ExecuteScalar<Byte[]>();
        }


        public class ResponseDocuSign
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }
    }


}