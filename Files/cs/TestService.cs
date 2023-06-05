using DocuSign.eSign.Client;
using static DocuSign.eSign.Client.Auth.OAuth;
using System;
using System.Linq;
using System.IO;
using System.ServiceModel;
using Terrasoft.Web.Common;
using Terrasoft.Core;
using System.ServiceModel.Activation;
using Terrasoft.Web.Common.ServiceRouting;
using System.ServiceModel.Web;
using System.Collections.Generic;

namespace Avt.DocuSignLib.Files.cs
{
    [ServiceContract]
    [DefaultServiceRoute]
    [SspServiceRoute]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Required)]
    public class TestServiceV2 : BaseService
    {

        public TestServiceV2() : base()
        {

        }

        public TestServiceV2(UserConnection userConnection) : base(userConnection)
        {

        }

        [OperationContract]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.Wrapped,
          RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        public void RunTest()
        {

            //var apiClient = new ApiClient("https://demo.docusign.net/restapi");
            ////string ik = "2296aa48-d2a9-4752-8271-c77ba1d9db99";
            ////string userId = "ff84a7a2-b2f4-451d-bfcc-27b150e9c05e";
            ////string account = "1e267202-150b-4a13-b845-a793517dcf84";
            ////string password = "76sufotO$";
            ////string envId = "12158a2f-76f5-4a7f-aa3a-1c89b21bcdfd";
            ////string authServer = "account-d.docusign.com";
            var data = (Byte[])Terrasoft.Core.Configuration.SysSettings.GetValue(UserConnection, "TextConnection");
            ////Stream stream = new MemoryStream((byte[])data);
            //////string rsaKey = System.IO.File.ReadAllText(@"private.key");
            //string authHeader = "{\"Username\":\"" + "i@sherled.ru" + "\",\"Password\":\"" + "76sufotO$" + "\",\"IntegratorKey\":\"" + "2296aa48-d2a9-4752-8271-c77ba1d9db99" + "\"}";
            //apiClient.Configuration.DefaultHeader.Add("X-DocuSign-Authentication", authHeader);


            var scopes = new List<string>
            {
                "signature"
            };

            //OAuth.OAuthToken authToken = apiClient.RequestJWTUserToken(
            //    "2296aa48-d2a9-4752-8271-c77ba1d9db99",
            //    "ff84a7a2-b2f4-451d-bfcc-27b150e9c05e",
            //    "account-d.docusign.com",
            //    data,
            //    1, scopes);

            //string accessToken = authToken.access_token;

            ////EnvelopesApi evelopensApi = new EnvelopesApi(apiClient);
            ////var l = evelopensApi.GetBasePath();
            ////var m = evelopensApi.CreateEnvelope(account);
            ////EnvelopeDocumentsResult result = evelopensApi.ListDocuments(account, envId);

            //Console.WriteLine(result);

            //EnvelopesApi evelopensApi = new EnvelopesApi(apiClient);
            //EnvelopeDocumentsResult result = evelopensApi.ListDocuments(account, envId);



            OAuthToken accessToken = JWTAuth.AuthenticateWithJWT("ESignature", "2296aa48-d2a9-4752-8271-c77ba1d9db99", "ff84a7a2-b2f4-451d-bfcc-27b150e9c05e",
                                                            "account-d.docusign.com", data);

            var docuSignClient = new DocuSignClient();
            docuSignClient.SetOAuthBasePath("account-d.docusign.com");
            UserInfo userInfo = docuSignClient.GetUserInfo(accessToken.access_token);
            var acct = userInfo.Accounts.FirstOrDefault();

            string signerEmail = "georgij999@mail.ru";
            string signerName = "Egor";
            string ccEmail = "i@sherled.ru";
            string ccName = "Sherled";
            string docDocx = Path.Combine(@"..", "..", "..", "..", "launcher-csharp", "World_Wide_Corp_salary.docx");
            string docPdf = Path.Combine(@"..", "..", "..", "..", "launcher-csharp", "World_Wide_Corp_lorem.pdf");
            Console.WriteLine("");
            var dataPdf = (Byte[])Terrasoft.Core.Configuration.SysSettings.GetValue(UserConnection, "TextConnectionPDF");
            var dataDoc = (Byte[])Terrasoft.Core.Configuration.SysSettings.GetValue(UserConnection, "TextConnectionDoc");
            string envelopeId = SigningViaEmail.SendEnvelopeViaEmail(signerEmail, signerName, ccEmail, ccName, accessToken.access_token, acct.BaseUri + "/restapi", acct.AccountId, dataDoc, dataPdf, "sent");
            Console.WriteLine($"Successfully sent envelope with envelopeId {envelopeId}");

        }
    }


}