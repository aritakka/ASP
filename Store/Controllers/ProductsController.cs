using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Models;
using System.Security.Claims;

namespace Store.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ProductsController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // 🔹 Список услуг (доступен всем)
        public async Task<IActionResult> Index()
        {
            var products = await _db.Products
                .Include(p => p.Category)
                .ToListAsync();

            return View(products);
        }

        // 🔹 Детали услуги
        public async Task<IActionResult> Details(int id)
        {
            var product = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductSuppliers)
                    .ThenInclude(ps => ps.Supplier)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            return View(product);
        }

        // 🔹 Создание новой услуги
        [HttpGet]
        public IActionResult Create()
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Supplier"))
                return Unauthorized();

            ViewBag.Categories = _db.Categories.ToList();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Product product, IFormFile? image)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Supplier"))
                return Unauthorized();

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = _db.Categories.ToList();
                return View(product);
            }

            _db.Products.Add(product);

            await _db.SaveChangesAsync();

            // 🔹 Привязка услуги к поставщику
            if (User.IsInRole("Supplier"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var supplier = await _db.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == userId);

                if (supplier == null)
                {
                    supplier = new Supplier
                    {
                        Id = userId,
                        Name = User.Identity?.Name ?? "Поставщик"
                    };

                    _db.Suppliers.Add(supplier);

                    await _db.SaveChangesAsync();
                }

                _db.ProductSuppliers.Add(new ProductSupplier
                {
                    ProductId = product.Id,
                    SupplierId = supplier.Id
                });

                await _db.SaveChangesAsync();
            }

            // 🔹 Сохранение изображения
            if (image != null)
            {
                var path = Path.Combine(_env.WebRootPath, "images/products");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                var filePath = Path.Combine(path, $"{product.Id}.jpg");

                using var stream = new FileStream(filePath, FileMode.Create);

                await image.CopyToAsync(stream);
            }

            return RedirectToAction(nameof(Index));
        }

        // 🔹 Редактирование услуги
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _db.Products
                .Include(p => p.ProductSuppliers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            if (User.IsInRole("Supplier"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                if (!product.ProductSuppliers.Any(ps => ps.SupplierId == userId))
                    return Unauthorized();
            }
            else if (!User.IsInRole("Admin"))
            {
                return Unauthorized();
            }

            ViewBag.Categories = _db.Categories.ToList();

            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Product product, IFormFile? image)
        {
            if (User.IsInRole("Supplier"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var ownsProduct = await _db.ProductSuppliers
                    .AnyAsync(ps => ps.ProductId == product.Id && ps.SupplierId == userId);

                if (!ownsProduct)
                    return Unauthorized();
            }
            else if (!User.IsInRole("Admin"))
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = _db.Categories.ToList();
                return View(product);
            }

            _db.Products.Update(product);

            await _db.SaveChangesAsync();

            // 🔹 Обновление изображения
            if (image != null)
            {
                var path = Path.Combine(_env.WebRootPath, "images/products");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                var filePath = Path.Combine(path, $"{product.Id}.jpg");

                using var stream = new FileStream(filePath, FileMode.Create);

                await image.CopyToAsync(stream);
            }

            return RedirectToAction(nameof(Index));
        }

        // 🔹 Удаление услуги
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products
                .Include(p => p.ProductSuppliers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            if (User.IsInRole("Supplier"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                if (!product.ProductSuppliers.Any(ps => ps.SupplierId == userId))
                    return Unauthorized();
            }
            else if (!User.IsInRole("Admin"))
            {
                return Unauthorized();
            }

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _db.Products.FindAsync(id);

            if (product == null)
                return NotFound();

            if (User.IsInRole("Supplier"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var ownsProduct = await _db.ProductSuppliers
                    .AnyAsync(ps => ps.ProductId == id && ps.SupplierId == userId);

                if (!ownsProduct)
                    return Unauthorized();
            }
            else if (!User.IsInRole("Admin"))
            {
                return Unauthorized();
            }

            _db.Products.Remove(product);

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}