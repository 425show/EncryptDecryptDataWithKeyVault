using System.Text;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

// vault URL could be passed as a parameter
var KeyVaultUrl = "https://cm-identity-kv.vault.azure.net";
// using Azure AD to support secretless authentication to Azure Key Vault
var credentials = new ChainedTokenCredential(
                        new AzureCliCredential(),
                        new ManagedIdentityCredential()
                );

var client = new KeyClient(new Uri(KeyVaultUrl), credentials);

//this could be parametarized as you may wish to pass different keys for different operations
var keyName = "MyEncryptionKey";

//get the key (or create one on the fly - very unlikely in a production environment)
KeyVaultKey key;
try 
{
    key = await client.GetKeyAsync(keyName); 
} 
catch (RequestFailedException ex) when (ex.Status == 404) 
{
    key = await client.CreateRsaKeyAsync(new CreateRsaKeyOptions(keyName));
} 

//get the crypto client of the key
var cryptoClient = client.GetCryptographyClient(key.Name, key.Properties.Version);

//do the fun stuff
var plainText = "My secret message";
var byteData = Encoding.Unicode.GetBytes(plainText);

Console.WriteLine("Encrypting...");
var encryptedResult = await cryptoClient.EncryptAsync(EncryptionAlgorithm.RsaOaep, byteData);
Console.WriteLine($"Encrypted data: {Convert.ToBase64String(encryptedResult.Ciphertext)}");
Console.WriteLine("Decrypting...");
var decryptedResult = await cryptoClient.DecryptAsync(EncryptionAlgorithm.RsaOaep, encryptedResult.Ciphertext);
Console.WriteLine($"Decrypted data: {Encoding.Unicode.GetString(decryptedResult.Plaintext)}");