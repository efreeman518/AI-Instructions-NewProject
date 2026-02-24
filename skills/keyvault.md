# Key Vault

## Prerequisites

- [package-dependencies.md](package-dependencies.md) — `EF.KeyVault` package types
- [configuration.md](configuration.md) — appsettings, secrets management, and Options pattern
- [identity-management.md](identity-management.md) — managed identity for Key Vault access
- [bootstrapper.md](bootstrapper.md) — centralized DI registration

## Overview

Key Vault access uses `EF.KeyVault` which provides abstractions over Azure Key Vault for **secrets**, **keys**, and **certificates** management, plus a **crypto utility** for encrypt/decrypt operations using vault-managed keys.

> **When to use Key Vault directly vs configuration:** Use the configuration provider (`Azure.Extensions.AspNetCore.Configuration.Secrets`) for secrets that map to app settings (connection strings, API keys). Use the `IKeyVaultManager` interface for runtime secret operations — dynamic secret creation, key rotation, certificate management, or when secrets need to be read/written programmatically by application logic.

---

## Key Vault Manager

### Interface

```csharp
public interface IKeyVaultManager
{
    // Secrets
    Task<string?> GetSecretAsync(string name, string? version = null,
        CancellationToken cancellationToken = default);
    Task<string?> SaveSecretAsync(string name, string? value = null,
        CancellationToken cancellationToken = default);
    Task<DeletedSecret> StartDeleteSecretAsync(string name,
        CancellationToken cancellationToken = default);

    // Keys
    Task<JsonWebKey?> GetKeyAsync(string name, string? version = null,
        CancellationToken cancellationToken = default);
    Task<JsonWebKey?> CreateKeyAsync(string name, KeyType keyType,
        CreateKeyOptions? options = null, CancellationToken cancellationToken = default);
    Task<KeyRotationPolicy> UpdateKeyRotationPolicyAsync(string name,
        KeyRotationPolicy policy, CancellationToken cancellationToken = default);
    Task<JsonWebKey?> RotateKeyAsync(string name,
        CancellationToken cancellationToken = default);
    Task<JsonWebKey?> DeleteKeyAsync(string name,
        CancellationToken cancellationToken = default);

    // Certificates
    Task<byte[]?> GetCertAsync(string certificateName,
        CancellationToken cancellationToken = default);
    Task<byte[]?> ImportCertAsync(ImportCertificateOptions importCertificateOptions,
        CancellationToken cancellationToken = default);
}
```

### Base Class

`KeyVaultManagerBase` implements `IKeyVaultManager` using three named Azure clients via `IAzureClientFactory`:
- `SecretClient` — for secrets
- `KeyClient` — for keys
- `CertificateClient` — for certificates

### Settings

```csharp
public class KeyVaultManagerSettingsBase
{
    public string KeyVaultClientName { get; set; } = null!;
}
```

### Concrete Implementation

```csharp
namespace {Project}.Infrastructure.Security;

public class {Project}KeyVaultManager : KeyVaultManagerBase, I{Project}KeyVaultManager
{
    public {Project}KeyVaultManager(
        ILogger<{Project}KeyVaultManager> logger,
        IOptions<{Project}KeyVaultManagerSettings> settings,
        IAzureClientFactory<SecretClient> secretClientFactory,
        IAzureClientFactory<KeyClient> keyClientFactory,
        IAzureClientFactory<CertificateClient> certClientFactory)
        : base(logger, settings, secretClientFactory, keyClientFactory, certClientFactory)
    {
    }
}

public interface I{Project}KeyVaultManager : IKeyVaultManager { }
public class {Project}KeyVaultManagerSettings : KeyVaultManagerSettingsBase { }
```

---

## Crypto Utility

For encrypt/decrypt operations using Key Vault-managed RSA keys:

### Interface

```csharp
public interface IKeyVaultCryptoUtility
{
    Task<byte[]> EncryptAsync(string plaintext);
    Task<string> DecryptAsync(byte[] ciphertext);
}
```

### Implementation

The package provides `KeyVaultCryptoUtility` which uses `CryptographyClient` with `EncryptionAlgorithm.RsaOaep`:

```csharp
public class KeyVaultCryptoUtility(CryptographyClient cryptoClient) : IKeyVaultCryptoUtility
{
    public async Task<byte[]> EncryptAsync(string plaintext)
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var encryptResult = await cryptoClient.EncryptAsync(EncryptionAlgorithm.RsaOaep, plaintextBytes);
        return encryptResult.Ciphertext;
    }

    public async Task<string> DecryptAsync(byte[] ciphertext)
    {
        var decryptResult = await cryptoClient.DecryptAsync(EncryptionAlgorithm.RsaOaep, ciphertext);
        return Encoding.UTF8.GetString(decryptResult.Plaintext);
    }
}
```

> **Key advantage:** The private key never leaves Key Vault — encryption/decryption operations happen server-side. This is ideal for PII, tokens, or sensitive field-level encryption.

---

## Configuration

### appsettings.json

```json
{
  "KeyVault": {
    "VaultUri": ""
  },
  "{Project}KeyVaultManagerSettings": {
    "KeyVaultClientName": "{Project}KVClient"
  }
}
```

### appsettings.Development.json

```json
{
  "KeyVault": {
    "VaultUri": "https://{project}-kv-dev.vault.azure.net/"
  }
}
```

---

## DI Registration (in Bootstrapper)

```csharp
private static void AddKeyVaultServices(IServiceCollection services, IConfiguration config)
{
    var vaultUri = new Uri(config["KeyVault:VaultUri"]!);

    // Register named Azure clients via IAzureClientFactory
    services.AddAzureClients(builder =>
    {
        builder.AddSecretClient(vaultUri)
            .WithName("{Project}KVClient");

        builder.AddKeyClient(vaultUri)
            .WithName("{Project}KVClient");

        builder.AddCertificateClient(vaultUri)
            .WithName("{Project}KVClient");

        // Use DefaultAzureCredential (managed identity in Azure, Visual Studio/CLI locally)
        builder.UseCredential(new DefaultAzureCredential());
    });

    // Bind settings
    services.Configure<{Project}KeyVaultManagerSettings>(
        config.GetSection("{Project}KeyVaultManagerSettings"));

    // Register KeyVault manager
    services.AddScoped<I{Project}KeyVaultManager, {Project}KeyVaultManager>();
}
```

### Crypto Utility Registration

```csharp
private static void AddKeyVaultCryptoServices(IServiceCollection services, IConfiguration config)
{
    var vaultUri = new Uri(config["KeyVault:VaultUri"]!);
    var keyName = config["KeyVault:EncryptionKeyName"]!;

    // CryptographyClient targets a specific key
    services.AddSingleton(sp =>
    {
        var keyClient = new KeyClient(vaultUri, new DefaultAzureCredential());
        return keyClient.GetCryptographyClient(keyName);
    });

    services.AddScoped<IKeyVaultCryptoUtility, KeyVaultCryptoUtility>();
}
```

---

## Service Layer Usage

### Runtime Secret Management

```csharp
public class ApiKeyService(
    I{Project}KeyVaultManager kvManager,
    ILogger<ApiKeyService> logger) : IApiKeyService
{
    public async Task<Result<string>> GetApiKeyAsync(
        string keyName, CancellationToken ct = default)
    {
        var secret = await kvManager.GetSecretAsync(keyName, cancellationToken: ct);
        return secret is not null
            ? Result<string>.Success(secret)
            : Result<string>.None();
    }

    public async Task<Result<string>> RotateApiKeyAsync(
        string keyName, CancellationToken ct = default)
    {
        var newKey = Guid.NewGuid().ToString("N");
        var saved = await kvManager.SaveSecretAsync(keyName, newKey, ct);
        logger.LogInformation("API key rotated: {KeyName}", keyName);
        return Result<string>.Success(saved!);
    }
}
```

### Field-Level Encryption

```csharp
public class SensitiveDataService(
    IKeyVaultCryptoUtility cryptoUtility,
    ILogger<SensitiveDataService> logger) : ISensitiveDataService
{
    public async Task<byte[]> EncryptSsnAsync(string ssn)
    {
        return await cryptoUtility.EncryptAsync(ssn);
    }

    public async Task<string> DecryptSsnAsync(byte[] encryptedSsn)
    {
        return await cryptoUtility.DecryptAsync(encryptedSsn);
    }
}
```

### Key Rotation with Policy

```csharp
public async Task SetupKeyRotationAsync(string keyName, CancellationToken ct = default)
{
    // Create RSA key
    await kvManager.CreateKeyAsync(keyName, KeyType.Rsa, new CreateRsaKeyOptions(keyName)
    {
        KeySize = 2048,
        ExpiresOn = DateTimeOffset.UtcNow.AddYears(1)
    }, ct);

    // Set rotation policy — rotate 30 days before expiry
    await kvManager.UpdateKeyRotationPolicyAsync(keyName, new KeyRotationPolicy
    {
        LifetimeActions =
        {
            new KeyRotationLifetimeAction(KeyRotationPolicyAction.Rotate)
            {
                TimeBeforeExpiry = TimeSpan.FromDays(30)
            }
        },
        ExpiresIn = "P365D"  // ISO 8601 duration
    }, ct);
}
```

### Certificate Management

```csharp
public async Task<X509Certificate2?> GetServiceCertAsync(
    string certName, CancellationToken ct = default)
{
    var certBytes = await kvManager.GetCertAsync(certName, ct);
    return certBytes is not null ? new X509Certificate2(certBytes) : null;
}
```

---

## Key Vault Access Patterns

| Pattern | Approach | When |
|---------|----------|------|
| **Config secrets** | `AzureKeyVaultConfigurationProvider` | Connection strings, API keys loaded at startup |
| **Runtime secrets** | `IKeyVaultManager.GetSecretAsync` | Dynamic secret lookup, user-specific keys |
| **Encryption** | `IKeyVaultCryptoUtility` | PII field encryption, token encryption |
| **Key rotation** | `IKeyVaultManager.RotateKeyAsync` + policy | Automated key lifecycle management |
| **Certificates** | `IKeyVaultManager.GetCertAsync` | mTLS, client certificates, signing |

---

## Security Considerations

1. **Managed identity** — Always use `DefaultAzureCredential` in production; never store Key Vault access keys in config
2. **Least privilege** — Grant only the specific Key Vault RBAC roles needed:
   - `Key Vault Secrets User` — read secrets
   - `Key Vault Secrets Officer` — read/write secrets
   - `Key Vault Crypto User` — encrypt/decrypt with keys
   - `Key Vault Certificates User` — read certificates
3. **Soft delete** — Always enable soft delete and purge protection on Key Vault
4. **Caching** — Cache secret values in memory for the duration they're needed; don't call Key Vault on every request
5. **Logging** — Never log secret values; the base class logs operation names only

---

## Verification

After generating Key Vault code, confirm:

- [ ] Manager inherits `KeyVaultManagerBase` with project-specific settings
- [ ] Settings class inherits `KeyVaultManagerSettingsBase` with `KeyVaultClientName`
- [ ] DI registers named `SecretClient`, `KeyClient`, `CertificateClient` via `IAzureClientFactory`
- [ ] `DefaultAzureCredential` used for authentication (not connection strings or access keys)
- [ ] Vault URI configured per environment (dev vs prod Key Vaults)
- [ ] Crypto utility registered with `CryptographyClient` targeting a specific key name
- [ ] Secret values never logged or returned in API responses
- [ ] Cross-references: [identity-management.md](identity-management.md) for managed identity setup; [iac.md](iac.md) for Key Vault provisioning with RBAC; [configuration.md](configuration.md) for startup config secrets
