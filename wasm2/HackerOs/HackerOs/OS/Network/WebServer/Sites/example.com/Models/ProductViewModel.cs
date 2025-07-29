using System.Collections.Generic;

namespace HackerOs.OS.Network.WebServer.Sites.example.com.Models
{
    public class ProductViewModel
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public bool IsInStock { get; set; }
        public List<CategoryModel> Categories { get; set; } = new List<CategoryModel>();
    }
    
    public class CategoryModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
