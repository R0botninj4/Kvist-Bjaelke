using DataAccess.Entities;
using Security.Dtos;

namespace Security.Services;

public interface IUserService
{
    User? TryLogin(TryLogin user);
    User TryRegister(CreateUserRequest user);
    List<User> GetUsers();
    User? GetUserById(int id);
    User UpdateUser(int id, UpdateUserRequest user);
    void DeleteUser(int id);
}
