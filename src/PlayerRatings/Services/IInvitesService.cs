using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PlayerRatings.Models;

namespace PlayerRatings.Services
{
    public interface IInvitesService
    {
        string GetInviteUrl(Guid inviteId, IUrlHelper urlHelper);
        Task SendEmail(Invite invite, IUrlHelper urlHelper);
        Task<ApplicationUser> Invite(string email, string displayName, string ranking, 
            string rankingOrganization, DateTimeOffset? rankingDate,
            string residence, int? birthYearValue, string photoUrl,
            ApplicationUser invitedBy, League league, IUrlHelper urlHelper);
    }
}
