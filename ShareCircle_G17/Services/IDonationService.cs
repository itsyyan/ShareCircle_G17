using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    public interface IDonationService
    {
        Task<List<DonationItem>> GetDonationsAsync();
        Task<DonationItem> GetDonationAsync(int id);
        Task<int> SaveDonationAsync(DonationItem item);
        Task<List<DonationItem>> GetUnsyncedDonationsAsync();
        Task<int> DeleteDonationAsync(DonationItem item);
        Task<List<DonationItem>> GetUserDonationsAsync(string userId);
    }
}
