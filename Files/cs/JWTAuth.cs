namespace Avt.DocuSignLib.Files.cs
{
    using System.Collections.Generic;
    using DocuSign.eSign.Client;
    using static DocuSign.eSign.Client.Auth.OAuth;

    public static class JWTAuth
    {
        public static OAuthToken AuthenticateWithJWT(string api, string clientId, string userId, string authServer, byte[] privateKeyBytes)
        {
            var docuSignClient = new DocuSignClient();
            var scopes = new List<string>
                {
                    "signature",
                    "impersonation",
                };

            return docuSignClient.RequestJWTUserToken(
                clientId,
                userId,
                authServer,
                privateKeyBytes,
                1,
                scopes);
        }
    }
}