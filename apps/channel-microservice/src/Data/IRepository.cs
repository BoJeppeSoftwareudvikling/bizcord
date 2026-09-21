namespace ChannelService.Data;

public interface IRepository<T>
{
    IEnumerable<T> GetAll();
    T? GetById(int id);
    T Create(T entity);
    void Update(T entity);
    void Delete(T entity);
}
