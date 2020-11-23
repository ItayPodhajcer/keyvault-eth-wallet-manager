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
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;

namespace EthereumWallets
{
    public static class CreateWallet
    {
        [FunctionName("CreateWallet")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "wallets")] HttpRequest req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var model = JsonConvert.DeserializeObject<CreateWalletModel>(requestBody);
            
            string KeyVaultUrl = Environment.GetEnvironmentVariable("KEY_VAULT_URL");

            var keyClient = new KeyClient(new Uri(KeyVaultUrl), new DefaultAzureCredential());

            var response = await keyClient.CreateEcKeyAsync(new CreateEcKeyOptions(model.Name, true) 
            {
                CurveName = KeyCurveName.P256K
            });
            
            var sha3 = new Sha3Keccack();
            var addressUtil = new AddressUtil();
            
            byte[] publicKey = response.Value.Key.ToECDsa().ExportSubjectPublicKeyInfo();
            byte[] hash = sha3.CalculateHash(publicKey);
            byte[] addressBuffer = new byte[hash.Length - 12];

            Array.Copy(hash, 12, addressBuffer, 0, hash.Length - 12);

            string address = addressUtil.ConvertToChecksumAddress(addressBuffer.ToHex());

            log.LogInformation($"Generate address: {address}");

            return new OkObjectResult(new CreateWalletResultModel { Address = address });
        }
    }

    public class CreateWalletModel
    {
        public string Name { get; set; }
    }

    public class CreateWalletResultModel
    {
        public string Address { get; set; }
    }
}
