using DataAccess.Entities;

namespace DataAccess.Repositories;

public interface IUserRepository
{
    User? GetUserByUsername(string username);
    User? GetById(int id);
    void CreateUser(User user);
    List<User> GetAll();
    void UpdateUser(User user);
    void DeleteUser(User user);
}
