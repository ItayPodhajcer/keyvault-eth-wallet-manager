using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace EthereumWallets
{
    public static class Sign
    {
        [FunctionName("Sign")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sign")] HttpRequest req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var model = JsonConvert.DeserializeObject<SignModel>(requestBody);

            string KeyVaultUrl = Environment.GetEnvironmentVariable("KEY_VAULT_URL");

            var keyClient = new KeyClient(new Uri(KeyVaultUrl), new DefaultAzureCredential());

            var response = await keyClient.GetKeyAsync(model.WalletName);
            
            var cryptoClient = new CryptographyClient(response.Value.Id, new DefaultAzureCredential());

            var signature = await cryptoClient.SignAsync(SignatureAlgorithm.ES256K, Convert.FromBase64String(model.Payload));

            return new OkObjectResult(new SignResultModel { Signature = Convert.ToBase64String(signature.Signature) });
        }
    }

    public class SignModel
    {
        public string WalletName { get; set; }
        public string Payload { get; set; }
    }

    public class SignResultModel
    {
        public string Signature { get; set; }
    }
}
