﻿using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Estimatorx.Core.Providers;
using Estimatorx.Core.Security;
using Estimatorx.Web.Models;
using Estimatorx.Web.Security;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using NLog.Fluent;

namespace Estimatorx.Web.Controllers
{
    [Authorize]
    [RequireHttps]
    public class AccountController : Controller
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly UserManager _userManager;
        private readonly SignInManager _signInManager;
        private readonly IAuthenticationManager _authenticationManager;
        
        private readonly ISampleGenerator _sampleGenerator;
        private readonly IProjectRepository _projectRepository;
        private readonly ITemplateRepository _templateRepository;

        public AccountController(
            UserManager userManager, 
            SignInManager signInManager, 
            IAuthenticationManager authenticationManager,
            ISampleGenerator sampleGenerator,
            IProjectRepository projectRepository,
            ITemplateRepository templateRepository
        )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _authenticationManager = authenticationManager;
            _sampleGenerator = sampleGenerator;
            _projectRepository = projectRepository;
            _templateRepository = templateRepository;
        }

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
                return View(model);

            SignInStatus result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                //case SignInStatus.LockedOut:
                //    return View("Lockout");
                //case SignInStatus.RequiresVerification:
                //    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid login attempt.");
                    return View(model);
            }
        }

        public ActionResult LogOut()
        {
            _authenticationManager.SignOut();
            return RedirectToAction("Index", "Home");
        }



        [AllowAnonymous]
        public ActionResult Register(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new User
            {
                UserName = model.Email.ToLowerInvariant(),
                Name = model.Name,
                Email = model.Email
            };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                AddErrors(result);
                return View(model);
            }

            await _signInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            CreateSamples(user);

            return RedirectToLocal(returnUrl);
        }



        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(EmailModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByNameAsync(model.Email);
            if (user == null /*|| !(await _userManager.IsEmailConfirmedAsync(user.Id))*/)
                // Don't reveal that the user does not exist or is not confirmed
                return View("ForgotPasswordConfirmation");

            // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771

            // Send an email with this link
            string code = await _userManager.GeneratePasswordResetTokenAsync(user.Id);
            var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code }, Request.Url.Scheme);

            string subject = "Reset Password";
            string body = "Please reset your password by clicking <a href=\"" + callbackUrl + "\">here</a>";
            
            await _userManager.SendEmailAsync(user.Id, subject, body);

            return RedirectToAction("ForgotPasswordConfirmation", "Account");
        }

        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }



        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return code == null ? View("Error") : View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordModel model)
        {
            if (!ModelState.IsValid)
                return View(model);
            
            var user = await _userManager.FindByNameAsync(model.Email);
            if (user == null)
                // Don't reveal that the user does not exist
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            
            var result = await _userManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
                return RedirectToAction("ResetPasswordConfirmation", "Account");
            
            AddErrors(result);
            return View();
        }

        [AllowAnonymous]
        public ActionResult ResetPasswordConfirmation()
        {
            return View();
        }


        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLogin(string provider, string returnUrl)
        {
            // Request a redirect to the external login provider
            return new ChallengeResult(provider, Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl }));
        }

        [AllowAnonymous]
        public async Task<ActionResult> ExternalLoginCallback(string returnUrl)
        {
            var loginInfo = await _authenticationManager.GetExternalLoginInfoAsync();
            if (loginInfo == null)
                return RedirectToAction("Login");

            // Sign in the user with this external login provider if the user already has a login
            var result = await _signInManager.ExternalSignInAsync(loginInfo, isPersistent: false);
            switch (result)
            {
                case SignInStatus.Success:
                    return RedirectToLocal(returnUrl);
                //case SignInStatus.LockedOut:
                //    return View("Lockout");
                //case SignInStatus.RequiresVerification:
                //    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = false });
                case SignInStatus.Failure:
                default:
                    // If the user does not have an account, then prompt the user to create an account
                    ViewBag.ReturnUrl = returnUrl;
                    ViewBag.LoginProvider = loginInfo.Login.LoginProvider;

                    var model = new ExternalRegisterModel()
                    {
                        Name = loginInfo.DefaultUserName,
                        Email = loginInfo.Email
                    };
                    
                    return View("ExternalLoginConfirmation", model);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExternalLoginConfirmation(ExternalRegisterModel model, string returnUrl)
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Profile");

            if (!ModelState.IsValid)
            {
                ViewBag.ReturnUrl = returnUrl;
                return View(model);
            }

            // Get the information about the user from the external login provider
            var info = await _authenticationManager.GetExternalLoginInfoAsync();
            if (info == null)
                return View("ExternalLoginFailure");

            var user = new User
            {
                UserName = model.Email,
                Name = model.Name,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                result = await _userManager.AddLoginAsync(user.Id, info.Login);
                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                    CreateSamples(user);
                                        
                    return RedirectToLocal(returnUrl);
                }
            }

            AddErrors(result);

            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkLogin(string provider)
        {
            // Request a redirect to the external login provider to link a login for the current user
            return new ChallengeResult(provider, Url.Action("LinkLoginCallback", "Account"), User.Identity.GetUserId());
        }

        public async Task<ActionResult> LinkLoginCallback()
        {
            var loginInfo = await _authenticationManager.GetExternalLoginInfoAsync(ChallengeResult.XsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
                return RedirectToAction("Index", "Profile", new { Message = "Error" });

            var result = await _userManager.AddLoginAsync(User.Identity.GetUserId(), loginInfo.Login);

            var message = result.Succeeded ? "Successful" : "Error";

            return RedirectToAction("Index", "Profile", new { Login = message });
        }


        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error);
        }

        private void CreateSamples(User user)
        {
            try
            {
                var project = _sampleGenerator.GenerateProject(user.Id);
                _projectRepository.Save(project);

                var template = _sampleGenerator.GenerateTemplate(user.Id);
                _templateRepository.Save(template);
            }
            catch (Exception ex)
            {
                _logger.Error()
                    .Message("Error Creating Samples: " + ex.Message)
                    .Exception(ex)
                    .Write();
            }

        }
    }
}