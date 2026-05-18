using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Data;
using Store.Models;
using System.Security.Claims;

namespace Store.Controllers
{
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 🔹 FIX: чтобы /Orders НЕ давал 404
        public IActionResult Index()
        {
            return RedirectToAction("MyOrders");
        }

        // GET: /Orders/MyOrders
        public async Task<IActionResult> MyOrders(bool justOrdered = false)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
                return RedirectToAction("Login", "Login");

            int userId = int.Parse(userIdClaim);

            var customer = await _context.Customers
                .Include(c => c.Orders)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                .Include(c => c.Orders)
                    .ThenInclude(o => o.Payment)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null)
            {
                var user = await _context.Users.FindAsync(userId);

                customer = new Customer
                {
                    UserId = userId,
                    FullName = user?.UserName ?? "User",
                    Email = user?.Email ?? "",
                    Phone = ""
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            var orders = customer.Orders
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            ViewBag.JustOrdered = justOrdered;

            return View(orders);
        }

        // POST: /Orders/CreateQuickOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuickOrder(int productId, int quantity = 1)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
                return RedirectToAction("Login", "Login");

            int userId = int.Parse(userIdClaim);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null)
            {
                var user = await _context.Users.FindAsync(userId);

                customer = new Customer
                {
                    UserId = userId,
                    FullName = user?.UserName ?? "User",
                    Email = user?.Email ?? "",
                    Phone = ""
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            var product = await _context.Products.FindAsync(productId);

            if (product == null)
                return NotFound();

            var order = new Order
            {
                CustomerId = customer.Id,
                OrderDate = DateTime.Now,
                TotalAmount = product.Price * quantity,
                Status = "New"
            };

            var orderItem = new OrderItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                Price = product.Price
            };

            order.OrderItems.Add(orderItem);
            _context.Orders.Add(order);

            var payment = new Payment
            {
                Order = order,
                Amount = order.TotalAmount,
                PaymentDate = DateTime.Now,
                PaymentMethod = "Cash",
                Status = "Pending"
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            return RedirectToAction("MyOrders", new { justOrdered = true });
        }

        // POST: /Orders/Pay
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int orderId)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId);

            if (payment != null)
            {
                payment.Status = "Completed";

                var order = await _context.Orders.FindAsync(orderId);

                if (order != null)
                    order.Status = "Completed";

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MyOrders");
        }
    }
}