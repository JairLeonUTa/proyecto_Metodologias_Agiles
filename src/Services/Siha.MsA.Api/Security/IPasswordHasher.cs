namespace Siha.MsA.Api.Security;

/// <summary>Abstracción de hashing de contraseñas (US-01, CA1: nunca en claro).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
