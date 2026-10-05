using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NewsPortal.Api.Models;
namespace NewsPortal.Api.Data;
public static class SeedData
{
    public static readonly string[] Roles = ["Administrator", "Editor", "Journalist", "Moderator", "User"];
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles)
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)));
        var password = config["Seed:Password"] ?? throw new InvalidOperationException("Set Seed__Password for development seed.");
        foreach (var role in Roles)
        {
            var email = role.ToLowerInvariant() + "@newsportal.local";
            if (await users.FindByEmailAsync(email) != null) continue;
            var user = new ApplicationUser { UserName = email, Email = email, FullName = "Demo " + role, EmailConfirmed = true };
            Check(await users.CreateAsync(user, password));
            Check(await users.AddToRoleAsync(user, role));
        }
        if (await db.Categories.AnyAsync()) return;
        string[] names = ["Thời sự", "Thế giới", "Kinh tế", "Công nghệ", "Giáo dục", "Sức khỏe", "Văn hóa", "Thể thao", "Du lịch", "Đời sống"];
        string[] slugs = ["thoi-su", "the-gioi", "kinh-te", "cong-nghe", "giao-duc", "suc-khoe", "van-hoa", "the-thao", "du-lich", "doi-song"];
        var categories = names.Select((name, i) => new Category { Name = name, Slug = slugs[i], DisplayOrder = i, Description = "Thông tin và góc nhìn về " + name.ToLower() }).ToArray();
        db.Categories.AddRange(categories);
        string[] tagNames = ["Việt Nam", "Đổi mới", "Cộng đồng", "Chuyển đổi số", "Môi trường", "Học đường", "Khởi nghiệp", "Hà Nội", "TP HCM", "Văn hóa Việt", "Sách", "Kỹ năng", "Giao thông", "Năng lượng", "Cuối tuần"];
        var tags = tagNames.Select((name, i) => new Tag { Name = name, Slug = "chu-de-" + (i + 1) }).ToArray();
        db.Tags.AddRange(tags);
        await db.SaveChangesAsync();
        var author = (await users.FindByEmailAsync("journalist@newsportal.local"))!;
        string[] titles = ["Không gian xanh mở ra nhịp sống mới", "Thư viện cộng đồng kết nối người yêu sách", "Sản phẩm địa phương đến gần người dùng", "Sinh viên xây dựng ứng dụng giao thông", "Lớp học mở khuyến khích tự học", "Đi bộ và thói quen sống chủ động", "Người trẻ tiếp nối nghề truyền thống", "Giải chạy lan tỏa tinh thần thể thao", "Khám phá những làng nghề ven đô", "Chợ cuối tuần và tiêu dùng bền vững", "Sân chơi từ ý tưởng của cư dân", "Trao đổi văn hóa qua dự án học đường", "Hợp tác xã tiếp cận thị trường mới", "Bảo vệ dữ liệu từ thói quen nhỏ", "Hướng nghiệp qua trải nghiệm thực tế", "Bữa ăn cân bằng từ nguyên liệu địa phương", "Triển lãm ảnh kể chuyện phố phường", "Câu lạc bộ thể thao cho người mới", "Du lịch chậm để tìm hiểu nơi đến", "Tái sử dụng đồ cũ trong không gian sống", "Tuyến đường an toàn trước cổng trường", "Sáng kiến kết nối thành phố và thiên nhiên", "Góc nhìn về kinh doanh tuần hoàn", "Phòng thực hành mở cho sáng tạo", "Đọc sách cùng trẻ trong kỳ nghỉ", "Không gian làm việc khuyến khích vận động", "Âm nhạc dân gian trong đời sống", "Tình nguyện viên đồng hành cùng giải đấu", "Tìm hiểu di sản qua bảo tàng", "Vườn rau ban công và niềm vui mỗi ngày", "Góp ý cho không gian công cộng", "Kết nối tri thức qua ngoại ngữ", "Quản lý chi tiêu cho người mới đi làm", "Công nghệ hỗ trợ tiếp cận thông tin", "Học qua dự án từ câu hỏi đến sản phẩm"];
        for (var i = 0; i < titles.Length; i++)
            db.Articles.Add(new Article {
                Title = titles[i], Slug = slugs[i % 10] + "-" + (i + 1), CategoryId = categories[i % 10].Id, AuthorId = author.Id,
                Summary = "Bài viết demo: " + titles[i] + ". Những ý tưởng gần gũi và giá trị thiết thực cho cộng đồng.",
                Content = titles[i] + "\n\nNội dung minh họa phục vụ đồ án NewsPortal, không phải bản tin đã được xác minh.\n\nTại buổi trao đổi cộng đồng, người tham gia chia sẻ những cách làm thiết thực, phù hợp với điều kiện địa phương. Các ý kiến tập trung vào việc bắt đầu từ những thay đổi nhỏ, lắng nghe nhu cầu thực tế và đánh giá kết quả thường xuyên.\n\nMột dự án bền vững cần sự phối hợp giữa nhiều bên. Công khai thông tin, ghi nhận phản hồi và điều chỉnh kế hoạch giúp hoạt động trở nên gần gũi hơn với người dân.\n\nBạn đọc có thể chia sẻ góc nhìn ở phần bình luận. Bình luận được kiểm duyệt trước khi hiển thị.",
                Status = i < 30 ? ArticleStatus.Published : i < 33 ? ArticleStatus.Draft : ArticleStatus.PendingReview,
                PublishedAt = i < 30 ? DateTime.UtcNow.AddHours(-i * 3) : null,
                IsFeatured = i % 7 == 0, IsBreakingNews = i == 0, IsEditorsChoice = i % 6 == 0,
                SeoTitle = titles[i], SeoDescription = "Bài viết minh họa cho đồ án NewsPortal.",
                ArticleTags = [new ArticleTag { TagId = tags[i % 15].Id }, new ArticleTag { TagId = tags[(i + 3) % 15].Id }]
            });
        db.SiteSettings.Add(new SiteSetting { Key = "SiteName", Value = "Nhịp Sống" });
        db.SiteSettings.Add(new SiteSetting { Key = "FooterText", Value = "NewsPortal · Đồ án Lập trình Web 2 · Nội dung demo" });
        await db.SaveChangesAsync();
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
