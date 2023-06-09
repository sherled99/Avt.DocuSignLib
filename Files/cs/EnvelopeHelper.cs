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
using System.Linq;
using Terrasoft.Core.Configuration;
using static Avt.DocuSignLib.Files.cs.EnvelopeHelper;
using System.Collections;

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
                int index = 0;
                foreach (var file in files)
                {
                    index++;
                    Document doc = new Document();
                    doc.DocumentBase64 = Convert.ToBase64String(file.File);
                    doc.Name = file.Name;
                    doc.DocumentId = index.ToString();
                    documents.Add(doc);
                    UpdateAvtEnvelopeAttachmentId(file.Id, index.ToString());
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

                var evp = envelopesApi.ListDocuments(account.AccountId, envelopeId);

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

        public ResponseEvp UpdateFiles(string envelopeId, Guid userId)
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
        
                var documentList = envelopesApi.ListDocuments(account.AccountId, envelopeId);
        
                var documents = documentList?.EnvelopeDocuments.Where(item => item.Type == "content");

                if (documents == default)
                {
                    logHelper.InsertLog("Documents are not here", UserConnection);
                    return new ResponseEvp
                    {
                        Success = true,
                        Message = "Documents are not here"
                    };
                }

                foreach(var document in documents)
                {
                    MemoryStream docStream = (MemoryStream)envelopesApi.GetDocument(account.AccountId, envelopeId, document.DocumentId);
                    CreateFileInCreatio(envelopeId, document, docStream);

                }

                logHelper.InsertLog("Documents Add", UserConnection);
        
                return new ResponseEvp
                {
                    Success = true,
                    Message = "Documents Add"
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

        private void CreateFileInCreatio(string envelopeDocuSignId, EnvelopeDocument document, MemoryStream docStream)
        {
            var envelopeAttachment = ReadAvtEnvelopeAttachment(envelopeDocuSignId, document.DocumentId);
            if (envelopeAttachment.AvtSysModuleSchemaName == default)
            {
                return;
            }
            var schemaProcess = UserConnection.AppConnection.SystemUserConnection.EntitySchemaManager.GetInstanceByName(envelopeAttachment.AvtSysModuleSchemaName + "File");
            var entityProcess = schemaProcess.CreateEntity(UserConnection);
            entityProcess.SetDefColumnValues();
            entityProcess.SetColumnValue("Name", document.Name);
            entityProcess.SetColumnValue("TypeId", Guid.Parse("529bc2f8-0ee0-df11-971b-001d60e938c6")); // File
            entityProcess.SetColumnValue(envelopeAttachment.AvtSysModuleSchemaName + "Id", envelopeAttachment.AvtRecordId);
            entityProcess.SetColumnValue("Uploaded", true);
            entityProcess.SetColumnValue("SysFileStorageId", Guid.Parse("38ab9812-9bba-4eb8-86d0-8f352cd0229c"));
            //entityProcess.SetColumnValue("FileGroupId", Guid.Parse("efbf3a0d-d780-465a-8e4b-8c0765197cfb"));
            entityProcess.SetColumnValue("Size", docStream.Length);
            entityProcess.SetColumnValue("Data", docStream.ToArray());
            entityProcess.Save(false);

            CreateAvtDocuSignAttachment(envelopeAttachment, entityProcess.PrimaryColumnValue, document.Name);
        }

        private void CreateAvtDocuSignAttachment(AvtEnvelopeAttachment envelopeAttachment, Guid recordId, string documentName)
        {
            var schemaProcess = UserConnection.AppConnection.SystemUserConnection.EntitySchemaManager.GetInstanceByName("AvtEnvelopeAttachment");
            var entityProcess = schemaProcess.CreateEntity(UserConnection);
            entityProcess.SetDefColumnValues();
            entityProcess.SetColumnValue("AvtSysModuleSchemaName", envelopeAttachment.AvtSysModuleSchemaName);
            entityProcess.SetColumnValue("AvtSchemaUId", envelopeAttachment.AvtSchemaUId);
            entityProcess.SetColumnValue("AvtPage", envelopeAttachment.AvtPage);
            entityProcess.SetColumnValue("AvtEnvelopeId", envelopeAttachment.AvtEnvelopeId);
            entityProcess.SetColumnValue("AvtRecrodName", envelopeAttachment.AvtRecrodName);
            entityProcess.SetColumnValue("AvtFileName", documentName);
            entityProcess.SetColumnValue("AvtRecordId", envelopeAttachment.AvtRecordId);
            entityProcess.SetColumnValue("AvtAttachmentId", recordId);
            entityProcess.Save(false);
        }

        private AvtEnvelopeAttachment ReadAvtEnvelopeAttachment(string envelopeDocuSignId, string documentId)
        {
            var select = new Select(UserConnection.AppConnection.SystemUserConnection)
                .Column("ea", "AvtSysModuleSchemaName")
                .Column("ea", "AvtAttachmentId")
                .Column("ea", "AvtRecordId")
                .Column("ea", "AvtSchemaUId")
                .Column("ea", "AvtPage")
                .Column("ea", "AvtEnvelopeId")
                .Column("ea", "AvtRecrodName")
                .From("AvtEnvelopeAttachment").As("ea")
                .InnerJoin("AvtEnvelope").As("e").On("e", "Id").IsEqual("ea", "AvtEnvelopeId")
                .Where("ea", "AvtDocuSignAttachment").IsEqual(Column.Const(documentId))
                .And("e", "AvtDocuSignEnvelope").IsEqual(Column.Const(envelopeDocuSignId))
            as Select;

            var envelopeAttachment = new AvtEnvelopeAttachment();

            using (DBExecutor executor = UserConnection.AppConnection.SystemUserConnection.EnsureDBConnection())
            {
                using (IDataReader reader = select.ExecuteReader(executor))
                {
                    int sysModuleSchemaNameColumnIndex = reader.GetOrdinal("AvtSysModuleSchemaName");
                    int attachmentIdColumnIndex = reader.GetOrdinal("AvtAttachmentId");
                    int recordIdColumnIndex = reader.GetOrdinal("AvtRecordId");
                    int schemaColumnIndex = reader.GetOrdinal("AvtSchemaUId");
                    int pageColumnIndex = reader.GetOrdinal("AvtPage");
                    int evpIdColumnIndex = reader.GetOrdinal("AvtEnvelopeId");
                    int recordNameColumnIndex = reader.GetOrdinal("AvtRecrodName");
                    while (reader.Read())
                    {
                        envelopeAttachment.AvtSysModuleSchemaName = reader.GetValue(sysModuleSchemaNameColumnIndex).ToString();
                        envelopeAttachment.AvtAttachmentId = reader.GetValue(attachmentIdColumnIndex).ToString();
                        envelopeAttachment.AvtRecordId = reader.GetValue(recordIdColumnIndex).ToString();
                        envelopeAttachment.AvtSchemaUId = reader.GetValue(schemaColumnIndex).ToString();
                        envelopeAttachment.AvtPage = reader.GetValue(pageColumnIndex).ToString();
                        envelopeAttachment.AvtEnvelopeId = (Guid)reader.GetValue(evpIdColumnIndex);
                        envelopeAttachment.AvtRecrodName = reader.GetValue(recordNameColumnIndex).ToString();


                    }
                }
            }

            return envelopeAttachment;
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

        private void UpdateAvtEnvelopeAttachmentId(Guid id, string number)
        {
            var update = new Update(UserConnection.AppConnection.SystemUserConnection, "AvtEnvelopeAttachment")
                .Set("AvtDocuSignAttachment", Column.Const(number))
                .Where("Id").IsEqual(Column.Const(id))
            as Update;
            update.Execute();
        }

        private List<FileObject> GetFiles(Guid envId)
        {
            var select = new Select(UserConnection.AppConnection.SystemUserConnection)
                .Column("AvtSysModuleSchemaName")
                .Column("AvtAttachmentId")
                .Column("Id")
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
                    int idColumnIndex = reader.GetOrdinal("Id");
                    while (reader.Read())
                    {
                        var moduleName = reader.GetValue(moduleNameColumnIndex).ToString();
                        var recordId = reader.GetValue(recordColumnIndex).ToString();
                        var id = (Guid)reader.GetValue(idColumnIndex);
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
                                    file.Id = id;
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
            public Guid Id { get; set; }
            public string Name { get; set; }

            public byte[] File { get; set; }
        }

        public class Envelope
        {
            public string EnvelopeDocuSignId {get; set;}
            public string Name { get; set; }
            public string Email { get; set; }
        }

        public class AvtEnvelopeAttachment
        {
            public string AvtSysModuleSchemaName { get; set; }
            public string AvtAttachmentId { get; set; }
            public string AvtRecordId { get; set; }
            public string AvtSchemaUId { get; set; }
            public string AvtPage { get; set; }
            public Guid AvtEnvelopeId { get; set; }
            public string AvtRecrodName { get; set; }
        }
    }
}
