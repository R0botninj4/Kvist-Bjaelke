using DataAccess.Entities;
using DataAccess.Repositories;
using Security.Dtos;
using Security.Services.Security;

namespace Security.Services.Implementation;

public class UserService(IUserRepository userRepo, IPasswordHasher passwordHasher) : IUserService
{
    public User? TryLogin(TryLogin user)
    {
        var dbUser = userRepo.GetUserByUsername(user.Username);
        if (dbUser == null) return null;
        return passwordHasher.VerifyPassword(user.Password, dbUser.PasswordHash) ? dbUser : null;
    }

    public User TryRegister(CreateUserRequest request)
    {
        var user = new User
        {
            Username = request.Username,
            PasswordHash = passwordHasher.HashAndSaltPassword(request.Password),
            Role = request.Role
        };
        userRepo.CreateUser(user);
        return user;
    }

    public List<User> GetUsers()
    {
        return userRepo.GetAll();
    }

    public User? GetUserById(int id)
    {
        return userRepo.GetById(id);
    }

    public User UpdateUser(int id, UpdateUserRequest request)
    {
        var user = userRepo.GetById(id)!;
        user.Username = request.Username;
        user.Role = request.Role;
        if (request.Password != null)
            user.PasswordHash = passwordHasher.HashAndSaltPassword(request.Password);
        userRepo.UpdateUser(user);
        return user;
    }

    public void DeleteUser(int id)
    {
        userRepo.DeleteUser(userRepo.GetById(id)!);
    }
}
