using DocuSign.eSign.Client;
using System.Linq;
using System.Text;
using static DocuSign.eSign.Client.Auth.OAuth;

namespace Avt.DocuSignLib.Files.cs
{
    public class GetTokenDocuSign
    {
        public OAuthToken GetOAuthToken(string userId, string clientId, string keyString)
        {
            return JWTAuth.AuthenticateWithJWT("ESignature", clientId, userId,
                "account-d.docusign.com", Encoding.ASCII.GetBytes(keyString));
        }

        public UserInfo.Account GetAccountDocuSign(OAuthToken oAuth)
        {
            var docuSignClient = new DocuSignClient();
            docuSignClient.SetOAuthBasePath("account-d.docusign.com");
            UserInfo userInfo = docuSignClient.GetUserInfo(oAuth.access_token);
            return userInfo.Accounts.FirstOrDefault();
        }
    }
}
