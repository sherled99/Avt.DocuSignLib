using DocuSign.eSign.Api;
using DocuSign.eSign.Client;
using System;
using Terrasoft.Core.DB;
using Terrasoft.Core;
using DocuSign.eSign.Model;
using System.Data;
using Terrosoft.Configuration;
using Terrasoft.Core.Factories;
using System.IO;
using Terrasoft.Core.Entities;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Avt.DocuSignLib.Files.cs
{
    [DefaultBinding(typeof(IEnvelopeHelper))]
    public class EnvelopeHelper : IEnvelopeHelper
    {
        private UserConnection UserConnection;

        public EnvelopeHelper(UserConnection userConnection)
        {
            UserConnection = userConnection;
        }

        public ResponseEvp CreateEnvelope(Guid envelopeId, Guid userId)
        {
            var logHelper = new LogHelper();
            try
            {
                var user = GetUserData(userId);
                var helper = new GetTokenDocuSign();
                var auth = helper.GetOAuthToken(user.UserId, user.IK, user.File);
                var token = auth.access_token;
                var account = helper.GetAccountDocuSign(auth);
                var avtEnvelope = GetAvtEnvelope(envelopeId);

                EnvelopeDefinition env = new EnvelopeDefinition();
                env.EmailSubject = avtEnvelope.Name;
                env.Status = "sent"; // created

                var roleList = GetRecipients(envelopeId);
                env.TemplateId = avtEnvelope.EnvelopeDocuSignId;
                env.TemplateRoles = roleList;

                List<Document> documents = new List<Document>();
                var files = GetFiles(envelopeId);
                int index = 1;
                foreach (var file in files)
                {
                    Document doc = new Document();
                    doc.DocumentBase64 = Convert.ToBase64String(file.File);
                    doc.Name = file.Name;
                    doc.DocumentId = index.ToString();
                    index++;
                    documents.Add(doc);
                }
                env.Documents = documents;

                var docuSignClient = new DocuSignClient(account.BaseUri + "/restapi");
                docuSignClient.Configuration.DefaultHeader.Add("Authorization", "Bearer " + token);
                EnvelopesApi envelopesApi = new EnvelopesApi(docuSignClient);
                EnvelopeSummary results = envelopesApi.CreateEnvelope(account.AccountId, env);
                logHelper.InsertLog(JsonConvert.SerializeObject(results), UserConnection);  

                UpdateEnvelope(results, envelopeId);

                return new ResponseEvp
                {
                    Success = true,
                    Message = JsonConvert.SerializeObject(results)
                };
            } catch (Exception ex)
            {
                logHelper.InsertLog(ex.Message, UserConnection);
                return new ResponseEvp
                {
                    Success = false,
                    Message = ex.Message
                };
            }
            
        }

        public ResponseEvp ReadEnvelope(string envelopeId, Guid userId)
        {
            var logHelper = new LogHelper();
            try
            {
                var user = GetUserData(userId);
                var helper = new GetTokenDocuSign();
                var auth = helper.GetOAuthToken(user.UserId, user.IK, user.File);
                var token = auth.access_token;
                var account = helper.GetAccountDocuSign(auth);
                var docuSignClient = new DocuSignClient(account.BaseUri + "/restapi");
                docuSignClient.Configuration.DefaultHeader.Add("Authorization", "Bearer " + token);
                EnvelopesApi envelopesApi = new EnvelopesApi(docuSignClient);
                var envInfo = envelopesApi.GetEnvelope(account.AccountId, envelopeId);

                logHelper.InsertLog(JsonConvert.SerializeObject(envInfo), UserConnection);
                return new ResponseEvp
                {
                    Success = true,
                    Message = JsonConvert.SerializeObject(envInfo)
                };
            }
            catch (Exception ex)
            {
                logHelper.InsertLog(ex.Message, UserConnection);
                return new ResponseEvp
                {
                    Success = false,
                    Message = ex.Message
                };
            }
            
        }

        private User GetUserData(Guid userId)
        {
            var select = new Select(UserConnection.AppConnection.SystemUserConnection)
                .Top(1)
                .Column("AvtDocuSignIntegrationKey")
                .Column("AvtUserId")
                .Column("AvtKeyNameFile")
                .From("AvtDocuSignUser")
                .Where("Id").IsEqual(Column.Const(userId))
            as Select;

            var user = new User();

            using (DBExecutor executor = UserConnection.AppConnection.SystemUserConnection.EnsureDBConnection())
            {
                using (IDataReader reader = select.ExecuteReader(executor))
                {
                    int ikNameColumnIndex = reader.GetOrdinal("AvtDocuSignIntegrationKey");
                    int userColumnIndex = reader.GetOrdinal("AvtUserId");
                    int fileColumnIndex = reader.GetOrdinal("AvtKeyNameFile");
                    while (reader.Read())
                    {
                        user.IK = reader.GetValue(ikNameColumnIndex).ToString();
                        user.UserId = reader.GetValue(userColumnIndex).ToString();
                        var fileObject = reader.GetValue(fileColumnIndex);
                        if (fileObject != null && fileObject != DBNull.Value)
                        {
                            byte[] byteArray = (byte[])fileObject;
                            Stream stream = new MemoryStream(byteArray);
                            StreamReader sReader = new StreamReader(stream);
                            string text = sReader.ReadToEnd();
                            user.File = text;
                        }

                    }
                }
            }

            return user;
        }

        private Envelope GetAvtEnvelope(Guid envelopeId)
        {
            var evp = new Envelope();

            var esq = new EntitySchemaQuery(UserConnection.AppConnection.SystemUserConnection.EntitySchemaManager, "AvtEnvelope");
            var nameColumnName = esq.AddColumn("AvtName").Name;
            var docuTemplateIdColumnName = esq.AddColumn("AvtTemplate.AvtDocuSignTemplateId").Name;
            var emailColumnName = esq.AddColumn("AvtOwner.Email").Name;
            var evelope = esq.GetEntity(UserConnection.AppConnection.SystemUserConnection, envelopeId);
            evp.EnvelopeDocuSignId = evelope.GetTypedColumnValue<string>(docuTemplateIdColumnName);
            evp.Name = evelope.GetTypedColumnValue<string>(nameColumnName);
            evp.Email = evelope.GetTypedColumnValue<string>(emailColumnName);

            return evp;
        }

        private void UpdateEnvelope(EnvelopeSummary results, Guid envelopeId)
        {
            var esq = new EntitySchemaQuery(UserConnection.AppConnection.SystemUserConnection.EntitySchemaManager, "AvtEnvelope");
            esq.AddAllSchemaColumns();
            var evelope = esq.GetEntity(UserConnection.AppConnection.SystemUserConnection, envelopeId);
            evelope.SetColumnValue("AvtDocuSignEnvelope", Guid.Parse(results.EnvelopeId));
            evelope.SetColumnValue("AvtStatusId", Guid.Parse("9e82cd78-5d34-4855-8bdc-bea49c2d719b")); // Sent
            evelope.Save(false);
        }

        private List<TemplateRole> GetRecipients(Guid envId)
        {
            var select = new Select(UserConnection.AppConnection.SystemUserConnection)
                .Column("c", "Email").As("Email")
                .Column("c", "Name").As("Name")
                .Column("er", "AvtRecipientRoleName")
                .From("AvtEnvelopeRecipient").As("er")
                .InnerJoin("Contact").As("c").On("c", "Id").IsEqual("er", "AvtContactId")
                .Where("er", "AvtEnvelopeId").IsEqual(Column.Const(envId))
            as Select;

            List<TemplateRole> rolesList = new List<TemplateRole>();

            using (DBExecutor executor = UserConnection.AppConnection.SystemUserConnection.EnsureDBConnection())
            {
                using (IDataReader reader = select.ExecuteReader(executor))
                {
                    
                    int roleColumnIndex = reader.GetOrdinal("AvtRecipientRoleName");
                    int emailColumnIndex = reader.GetOrdinal("Email");
                    int nameColumnIndex = reader.GetOrdinal("Name");
                    while (reader.Read())
                    {
                        TemplateRole tRole = new TemplateRole();                        
                        tRole.Email = reader.GetValue(emailColumnIndex).ToString();
                        tRole.Name = reader.GetValue(nameColumnIndex).ToString();
                        tRole.RoleName = reader.GetValue(roleColumnIndex).ToString();
                        rolesList.Add(tRole);
                    }
                }
            }

            return rolesList;
        }

        private List<FileObject> GetFiles(Guid envId)
        {
            var select = new Select(UserConnection.AppConnection.SystemUserConnection)
                .Column("AvtSysModuleSchemaName")
                .Column("AvtAttachmentId")
                .From("AvtEnvelopeAttachment")
                .Where("AvtEnvelopeId").IsEqual(Column.Const(envId))
            as Select;

            List<FileObject> fileList = new List<FileObject>();

            using (DBExecutor executor = UserConnection.AppConnection.SystemUserConnection.EnsureDBConnection())
            {
                using (IDataReader reader = select.ExecuteReader(executor))
                {
                    int moduleNameColumnIndex = reader.GetOrdinal("AvtSysModuleSchemaName");
                    int recordColumnIndex = reader.GetOrdinal("AvtAttachmentId");
                    while (reader.Read())
                    {
                        var moduleName = reader.GetValue(moduleNameColumnIndex).ToString();
                        var recordId = reader.GetValue(recordColumnIndex).ToString();
                        var selectFile = new Select(UserConnection.AppConnection.SystemUserConnection)
                            .Top(1)
                            .Column("Data")
                            .Column("Name")
                            .From($"{moduleName}File")
                            .Where("Id").IsEqual(Column.Const(recordId))
                        as Select;

                        using (DBExecutor executorFile = UserConnection.AppConnection.SystemUserConnection.EnsureDBConnection())
                        {
                            using (IDataReader readerFile = selectFile.ExecuteReader(executorFile))
                            {
                                int dataColumnIndex = readerFile.GetOrdinal("Data");
                                int nameColumnIndex = readerFile.GetOrdinal("Name");
                                while (readerFile.Read())
                                {
                                    FileObject file = new FileObject();
                                    file.Name = readerFile.GetValue(nameColumnIndex).ToString();

                                    var fileObject = readerFile.GetValue(dataColumnIndex);
                                    if (fileObject != null && fileObject != DBNull.Value)
                                    {
                                        byte[] byteArray = (byte[])fileObject;
                                        file.File = byteArray;
                                    }
                                    
                                    fileList.Add(file);
                                }
                            }
                        }
                    }
                }
            }

            return fileList;
        }

        public class User
        {
            public string IK { get; set; }
            public string UserId { get; set; }

            public string File { get; set; }
        }

        public class FileObject
        {
            public string Name { get; set; }

            public byte[] File { get; set; }
        }

        public class Envelope
        {
            public string EnvelopeDocuSignId {get; set;}
            public string Name { get; set; }
            public string Email { get; set; }
        }

    }
}
