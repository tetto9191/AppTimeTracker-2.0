using AppTimeTracker.Data.Models;
namespace AppTimeTracker.Data.Repositories;

public interface IConfigRepository
{
    AppConfig Load();
    void Save(AppConfig config);
}