using TTSmartEcom.Application.Users;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Repositories.Users;

public sealed class MongoPasswordHashWriter : IPasswordHashWriter
{
    public string Hash(string password) => global::BCrypt.Net.BCrypt.HashPassword(password, 10);
}
