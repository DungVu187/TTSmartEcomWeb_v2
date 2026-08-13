namespace TTSmartEcom.Application.Users;

public interface IPasswordHashWriter
{
    string Hash(string password);
}
