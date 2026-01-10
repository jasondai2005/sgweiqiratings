using PlayerRatings.Models;
using PlayerRatings.Services;
using PlayerRatings.Util;
using PlayerRatings.ViewModels.Invites;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PlayerRatings.Localization;
using PlayerRatings.Repositories;

namespace PlayerRatings.Controllers
{
    [Authorize]
    public class InvitesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IInvitesService _invitesService;
        private readonly ILeaguesRepository _leaguesRepository;
        private readonly IStringLocalizer<InvitesController> _localizer;

        public InvitesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IInvitesService invitesService, ILeaguesRepository leaguesRepository, IStringLocalizer<InvitesController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _invitesService = invitesService;
            _leaguesRepository = leaguesRepository;
            _localizer = localizer;
        }

        // GET: Invites
        public async Task<IActionResult> Index()
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            // Only admins can access invites
            var adminLeagues = _leaguesRepository.GetAdminAuthorizedLeagues(currentUser);
            if (!adminLeagues.Any())
            {
                return NotFound();
            }

            return View(await _context.Invites.AsNoTracking().Include(i => i.CreatedUser).Where(i => i.InvitedById == currentUser.Id).ToListAsync());
        }

        // GET: Invites/Create
        public async Task<IActionResult> Create(Guid? leagueId)
        {
            var currentUser = await User.GetApplicationUser(_userManager);
            
            // Only admins can create invites - get admin-authorized leagues only
            var leagues = _leaguesRepository.GetAdminAuthorizedLeagues(currentUser).ToList();

            if (!leagues.Any())
            {
                return RedirectToAction("NoLeagues", "Leagues");
            }

            if (leagueId.HasValue && _leaguesRepository.GetAdminAuthorizedLeague(currentUser, leagueId.Value) == null)
            {
                return NotFound();
            }

            return View(new InviteViewModel
            {
                LeagueId = leagueId,
                Leagues = leagues
            });
        }

        // POST: Invites/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InviteViewModel invite)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            // Only admins can create invites - get admin-authorized leagues only
            invite.Leagues = _leaguesRepository.GetAdminAuthorizedLeagues(currentUser);

            if (!invite.Leagues.Any())
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(invite);
            }

            League league = null;
            if (invite.LeagueId.HasValue)
            {
                league = _leaguesRepository.GetAdminAuthorizedLeague(currentUser, invite.LeagueId.Value);

                if (league == null)
                {
                    ModelState.AddModelError("", _localizer[nameof(LocalizationKey.LeagueNotFound)]);
                    return View(invite);
                }
            }

            try
            {
                DateTimeOffset? rankingDate = invite.RankingDate.HasValue 
                    ? new DateTimeOffset(invite.RankingDate.Value, TimeSpan.Zero) 
                    : null;
                await _invitesService.Invite(invite.Email, invite.DisplayName, invite.Ranking, 
                    invite.RankingOrganization, rankingDate,
                    invite.Residence, invite.BirthYearValue, invite.PhotoUrl,
                    currentUser, league, Url);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(invite);
            }

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);

            // Only admins can edit invites
            var adminLeagues = _leaguesRepository.GetAdminAuthorizedLeagues(currentUser);
            if (!adminLeagues.Any())
            {
                return NotFound();
            }

            var invite = await _context.Invites.Include(i => i.CreatedUser).SingleAsync(m => m.Id == id);
            if (invite == null || invite.InvitedById != currentUser.Id)
            {
                return NotFound();
            }

            return View(new InviteViewModel
            {
                Email = invite.CreatedUser.Email
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(InviteViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await User.GetApplicationUser(_userManager);

                // Only admins can edit invites
                var adminLeagues = _leaguesRepository.GetAdminAuthorizedLeagues(currentUser);
                if (!adminLeagues.Any())
                {
                    return NotFound();
                }

                var invite = await _context.Invites.Include(i => i.CreatedUser).SingleAsync(m => m.Id == model.Id);
                if (invite == null || invite.InvitedById != currentUser.Id)
                {
                    return NotFound();
                }

                var createdUser = invite.CreatedUser;

                createdUser.Email = model.Email;
                createdUser.DisplayName = model.Email;
                createdUser.UserName = model.Email;

                await _userManager.UpdateAsync(createdUser);

                await _invitesService.SendEmail(invite, Url);

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Invites/Delete/5
        [ActionName("Delete")]
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUser = await User.GetApplicationUser(_userManager);
            
            // Only admins can delete invites
            var adminLeagues = _leaguesRepository.GetAdminAuthorizedLeagues(currentUser);
            if (!adminLeagues.Any())
            {
                return NotFound();
            }

            var invite = await _context.Invites.AsNoTracking().Include(i => i.CreatedUser).SingleOrDefaultAsync(m => m.Id == id);
            if (invite == null)
            {
                return NotFound();
            }

            return View(invite);
        }

        // POST: Invites/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var currentUser = await User.GetApplicationUser(_userManager);
            
            // Only admins can delete invites
            var adminLeagues = _leaguesRepository.GetAdminAuthorizedLeagues(currentUser);
            if (!adminLeagues.Any())
            {
                return NotFound();
            }

            var invite = await _context.Invites.SingleAsync(m => m.Id == id);
            _context.Invites.Remove(invite);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
