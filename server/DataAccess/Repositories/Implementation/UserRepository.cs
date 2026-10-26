using DataAccess.Entities;

namespace DataAccess.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public User? GetUserByUsername(string username)
    {
        return _dbContext.Users.FirstOrDefault(user => user.Username == username);
    }

    public User? GetById(int id)
    {
        return _dbContext.Users.Find(id);
    }

    public void CreateUser(User user)
    {
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();
    }

    public List<User> GetAll()
    {
        return _dbContext.Users.ToList();
    }

    public void UpdateUser(User user)
    {
        _dbContext.Users.Update(user);
        _dbContext.SaveChanges();
    }

    public void DeleteUser(User user)
    {
        _dbContext.Users.Remove(user);
        _dbContext.SaveChanges();
    }
}
