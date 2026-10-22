public class CategoryService
{
    private readonly AdoNetDbService _db;

    public CategoryService(AdoNetDbService db)
    {
        _db = db;
    }

    public List<Category> GetAll() => _db.GetAllCategories();

    public List<string> GetAllNames()
    {
        var cats = _db.GetAllCategories();
        if (cats.Count == 0)
            return [.. Catalog.Categories];
        return cats.Select(c => c.Name).ToList();
    }

    public bool Add(string name) => _db.AddCategory(name);

    public bool Delete(int id) => _db.DeleteCategory(id);
}
