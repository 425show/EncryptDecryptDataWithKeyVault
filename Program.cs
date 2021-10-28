using System.Text;
using Azure;
using Azure.Core.Cryptography;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

var StorageUri = @"https://cmdemo20210224.blob.core.windows.net/sample-data";
var KeyVaultUrl = "https://cm-identity-kv.vault.azure.net";
var FilePath = @"C:\Users\chmatsk\Downloads\ticket.pdf";
var DownloadPath = @"C:\Users\chmatsk\Downloads\StorageFiles";
var keyName = "MyEncryptionKey";

// using Azure AD to support secretless authentication to Azure Key Vault
var credentials = new ChainedTokenCredential(
                        new AzureCliCredential(),
                        new ManagedIdentityCredential()
                );

var client = new KeyClient(new Uri(KeyVaultUrl), credentials);

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

var blobEncryptionOptions = new ClientSideEncryptionOptions(ClientSideEncryptionVersion.V1_0)
{
    KeyResolver = new KeyResolver(credentials),
    KeyEncryptionKey = cryptoClient,
    KeyWrapAlgorithm = KeyWrapAlgorithm.RsaOaep.ToString()
};

BlobClientOptions blobOptions = new SpecializedBlobClientOptions() 
{
    ClientSideEncryption = blobEncryptionOptions 
};

var containerClient = new BlobContainerClient(
        new Uri(StorageUri), 
        credentials,
        blobOptions
);

var blobName = Path.GetFileName(FilePath);
Console.WriteLine("Encrypting file and uploading to storage");
await EncryptAndUploadData(StorageUri,FilePath, blobName);
Console.WriteLine("Upload Complete!");

Console.WriteLine("Decrypting file and downloading from storage");
await DecryptAndDownloadData(blobName);
Console.WriteLine("Download Complete!");

async Task EncryptAndUploadData(string storageUri, string FilePath, string blobName)
{
    var byteData = File.ReadAllBytes(FilePath);
    await containerClient.UploadBlobAsync(blobName, new MemoryStream(byteData)); 
}

async Task DecryptAndDownloadData(string blobName)
{
    var blobClient = containerClient.GetBlobClient(blobName);
    await blobClient.DownloadToAsync(Path.Combine(DownloadPath,blobName));
}