using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PlayerRatings.Localization
{
    //To sort static fields you can use resharper, for example.
    //Go to Resharper->Options->Code Editing->C#->File Layout
    //Choose Static Fields and Constants
    //Select "Sort By" - Access. And then by Name
    //Call Resharper->Tools->Clean up
    //http://stackoverflow.com/questions/1509244/resharper-clean-up-code-how-to-affect-sorting-of-methods#answer-31734205
    public class LocalizationKey
    {
        public static readonly LocalizationKey AddAnotherServiceToLogin =
            new LocalizationKey("Add another service to log in", "添加使用其他服务的登录方式");

        public static readonly LocalizationKey AddNewResult = new LocalizationKey("Add new result", "记录比赛结果");

        public static readonly LocalizationKey AddThisAndAnotherOne = new LocalizationKey("Add this and another one", "添加并继续");

        public static readonly LocalizationKey AddThisAndGoToRating = new LocalizationKey("Add and go to the rating", "添加并查看等级分");

        public static readonly LocalizationKey AgainstFor = new LocalizationKey("Goals Against / For", "得分/丢分");

        public static readonly LocalizationKey AreYouSureDelete =
            new LocalizationKey("Are you sure you want to delete this", "请确认删除");

        public static readonly LocalizationKey AssociateForm = new LocalizationKey("Association Form", "关联表格");

        public static readonly LocalizationKey AssociateYourAccount = new LocalizationKey("Associate your {0} account", "关联您的{0}帐号");

        public static readonly LocalizationKey BackToList = new LocalizationKey("Back to list", "返回列表");
        public static readonly LocalizationKey Block = new LocalizationKey("Block", "阻止");
        public static readonly LocalizationKey ChangePassword = new LocalizationKey("Change Password", "修改密码");

        public static readonly LocalizationKey ChangePasswordForm = new LocalizationKey("Change Password Form", "修改密码表格");

        public static readonly LocalizationKey ChangeYourAccountSettings =
            new LocalizationKey("Change your account settings", "修改帐号设置");

        public static readonly LocalizationKey CheckEmailToReset =
            new LocalizationKey("Please check your email to reset your password", "请查看邮件以修改密码");

        public static readonly LocalizationKey ClickHereToLogin = new LocalizationKey("Click here to Log in", "点击登陆");

        public static readonly LocalizationKey ConfirmAccount =
            new LocalizationKey("Please confirm your account by clicking this link: <a href=\"{0}\">{0}</a>", "请点击链接<a href=\"{0}\">{0}</a>确认帐号");

        public static readonly LocalizationKey ConfirmEmail = new LocalizationKey("Confirm Email", "确认邮件");

        public static readonly LocalizationKey ConfirmPassword = new LocalizationKey("Confirm password", "确认密码");

        public static readonly LocalizationKey Create = new LocalizationKey("Create", "创建");
        public static readonly LocalizationKey CreateNew = new LocalizationKey("Create New", "新建");

        public static readonly LocalizationKey CreateNewAccount = new LocalizationKey("Create a new account", "创建新帐号");

        public static readonly LocalizationKey CreateYourLeague = new LocalizationKey("Create your league", "创建您的联赛");

        public static readonly LocalizationKey Date = new LocalizationKey("Date", "日期");

        public static readonly LocalizationKey DateIndex = new LocalizationKey("Index of Date column", "日期列号码");

        public static readonly LocalizationKey DateTimeFormat = new LocalizationKey("Date time format", "日期时间格式");

        public static readonly LocalizationKey Delete = new LocalizationKey("Delete", "删除");
        public static readonly LocalizationKey Details = new LocalizationKey("Details", "详细信息");

        public static readonly LocalizationKey DisplayName = new LocalizationKey("Display name", "名字");
        public static readonly LocalizationKey Username = new LocalizationKey("Username", "用户名");
        public static readonly LocalizationKey BirthYear = new LocalizationKey("Birth Year", "生年");
        public static readonly LocalizationKey Ranking = new LocalizationKey("Ranking", "段/级位");
        public static readonly LocalizationKey RankedDate = new LocalizationKey("Ranked Date", "升段/级时间");
        public static readonly LocalizationKey OriRanking = new LocalizationKey("Original Ranking", "原段位");
        public static readonly LocalizationKey Edit = new LocalizationKey("Edit", "编辑");
        public static readonly LocalizationKey Elo = new LocalizationKey("Elo", "等级分");
        public static readonly LocalizationKey Email = new LocalizationKey("Email", "邮箱");
        public static readonly LocalizationKey ShiftRating = new LocalizationKey("Shift Rating", "等级分值");
        public static readonly LocalizationKey RankingHistory = new LocalizationKey("Ranking History", "升段/级纪录");

        public static readonly LocalizationKey EnterYourEmail = new LocalizationKey("Enter your email", "请输入您的邮箱");

        public static readonly LocalizationKey Error = new LocalizationKey("Error", "错误");

        public static readonly LocalizationKey ErrorOccurred = new LocalizationKey("An error has occurred", "有错误发生");

        public static readonly LocalizationKey ErrorOccurredWhileProcessing =
            new LocalizationKey("An error occurred while processing your request", "处理您的请求时出错");

        public static readonly LocalizationKey ExternalLoginAdded = new LocalizationKey("The external login was added", "已添加外部登录方式");

        public static readonly LocalizationKey ExternalLoginRemoved =
            new LocalizationKey("The external login was removed", "已删除外部登录方式");

        public static readonly LocalizationKey ExternalLogins = new LocalizationKey("External Logins", "外部登录");

        public static readonly LocalizationKey ExternalRegisterSuccessInstuction =
            new LocalizationKey(
                "Please enter a name for this site below and click the Register button to finish logging in",
                "请在下方输入您的名字，然后点击注册按钮完成登录");

        public static readonly LocalizationKey FactorIndex = new LocalizationKey("Index of Factor column", "仅值列号码");

        public static readonly LocalizationKey Factor = new LocalizationKey("Factor", "权值");

        public static readonly LocalizationKey File = new LocalizationKey("File", "文件");
        public static readonly LocalizationKey FirstPlayer = new LocalizationKey("First player", "选手一");

        public static readonly LocalizationKey FirstPlayerEmailIndex =
            new LocalizationKey("Index of First Player Email column", "选手一邮箱列号码");

        public static readonly LocalizationKey FirstPlayerScore = new LocalizationKey("First player score", "选手一得分");

        public static readonly LocalizationKey FirstPlayerScoreIndex =
            new LocalizationKey("Index of First Player Score column", "选手一得分列号码");

        public static readonly LocalizationKey Forecast = new LocalizationKey("Forecast", "预报");

        public static readonly LocalizationKey ForgotPasswordConfirmation =
            new LocalizationKey("Forgot Password Confirmation", "忘记密码确认");

        public static readonly LocalizationKey ForgotYourPassword = new LocalizationKey("Forgot your password", "忘记密码");

        public static readonly LocalizationKey HasNoConfiguredServices =
            new LocalizationKey("Authentication services was not configured", "未配置认证服务");

        public static readonly LocalizationKey Hello = new LocalizationKey("Hello", "您好");

        public static readonly LocalizationKey Import = new LocalizationKey("Import", "导入");
        
        public static readonly LocalizationKey ImportFromH9 = new LocalizationKey("Import Results", "导入成绩");

        public static readonly LocalizationKey ImportData =
            new LocalizationKey(
                "You can import your matches in csv format. Each record must contain Date, First Player Email, Second Player Email, First Player Score, Second Player Score and optionally Factor. Unknown users will be invited to league automatically",
                "您可以导入csv格式的比赛数据。每条数据必须包含以下各列：日期、选手一邮箱、选手二邮箱、选手一得分、选手二得分，还可包含一个可选列因子。未知选手将被自动邀请进联赛。");

        public static readonly LocalizationKey ImportMatches = new LocalizationKey("Import matches", "导入比赛数据");

        public static readonly LocalizationKey InvitationForm = new LocalizationKey("Invintation Form", "邀请选手");

        public static readonly LocalizationKey Invite = new LocalizationKey("Invite", "邀请");

        public static readonly LocalizationKey InvitedYou =
            new LocalizationKey("{0} invited you to join the rating system", "{0}邀请您加入等级分系统");

        public static readonly LocalizationKey InviteNew =
            new LocalizationKey("Invite new player", "邀请新选手");

        public static readonly LocalizationKey Invites = new LocalizationKey("Invites", "邀请");
        public static readonly LocalizationKey LastMatches = new LocalizationKey("Last matches", "最新赛事");
        public static readonly LocalizationKey League = new LocalizationKey("League", "联赛");

        public static readonly LocalizationKey LeagueNotFound =
            new LocalizationKey("Can not find league or you don't have access", "找不到联赛或您没有访问权限");

        public static readonly LocalizationKey Leagues = new LocalizationKey("Leagues", "联赛");
        public static readonly LocalizationKey LockedOut = new LocalizationKey("Locked out", "锁出");

        public static readonly LocalizationKey LockedOutTryLater =
            new LocalizationKey("This account has been locked out, please try again later", "此帐号已被锁定，请稍后再试");

        public static readonly LocalizationKey LogIn = new LocalizationKey("Log in", "登录");
        public static readonly LocalizationKey LoginFailure = new LocalizationKey("Login Failure", "登录失败");

        public static readonly LocalizationKey LogInUsingExternal = new LocalizationKey(
            "Log in using your {0} account", "使用您的{0}帐号登录");

        public static readonly LocalizationKey LogOff = new LocalizationKey("Log off", "登出");
        public static readonly LocalizationKey LooseStreak = new LocalizationKey("Lose streak", "连败");
        public static readonly LocalizationKey Manage = new LocalizationKey("Manage", "管理");

        public static readonly LocalizationKey ManageYourAccount = new LocalizationKey("Manage your account", "管理帐号");

        public static readonly LocalizationKey ManageYourExternalLogins =
            new LocalizationKey("Manage your external logins", "管理外部登录");

        public static readonly LocalizationKey Match = new LocalizationKey("Match", "比赛");
        public static readonly LocalizationKey Matches = new LocalizationKey("Matches", "比赛");
        public static readonly LocalizationKey Message = new LocalizationKey("Message", "消息");
        public static readonly LocalizationKey NewPassword = new LocalizationKey("New password", "新密码");

        public static readonly LocalizationKey NoLeagues =
            new LocalizationKey("You have no leagues", "您还没有联赛");

        public static readonly LocalizationKey OldPassword = new LocalizationKey("Old password", "旧密码");

        public static readonly LocalizationKey Password = new LocalizationKey("Password", "密码");

        public static readonly LocalizationKey PasswordChanged = new LocalizationKey("Your password has been changed", "您的密码已修改");

        public static readonly LocalizationKey PasswordSet = new LocalizationKey("Your password has been set", "密码已设置");

        public static readonly LocalizationKey PlayerNotFound =
            new LocalizationKey("Can not find player or you don't have access", "找不到选手或您没有访问权限");

        public static readonly LocalizationKey Players =
            new LocalizationKey("Players", "选手");

        public static readonly LocalizationKey Rating = new LocalizationKey("Rating", "等级分");
        public static readonly LocalizationKey ShowSinRankingsOnly = new LocalizationKey("Only show Singapore rankings", "仅显示新加坡段、级位");
        public static readonly LocalizationKey ShowAllRankings = new LocalizationKey("Show all rankings", "显示所有段、级位");
        public static readonly LocalizationKey ProtectedRatingsSupported = new LocalizationKey("Protected ratings option is on", "等级分保护已开启");
        public static readonly LocalizationKey ProtectedRatingsNotSupported = new LocalizationKey("Protected ratings option is off", "等级分保护未开启");
        public static readonly LocalizationKey HistoryRating = new LocalizationKey("History Rating", "历史等级分");

        public static readonly LocalizationKey RatingSource = new LocalizationKey("Source of rating", "等级分来源");

        public static readonly LocalizationKey Register = new LocalizationKey("Register", "注册");

        public static readonly LocalizationKey RegisteredLogins = new LocalizationKey("Registered Logins", "已添加登录方式");

        public static readonly LocalizationKey RegisterNewUser = new LocalizationKey("Register as a new user", "注册新用户");

        public static readonly LocalizationKey RememberMe = new LocalizationKey("Remember me", "记住我");
        public static readonly LocalizationKey Remove = new LocalizationKey("Remove", "移除");

        public static readonly LocalizationKey RemoveExternalFrom =
            new LocalizationKey("Remove this {0} login from your account", "从您的帐号中移除{0}登录");

        public static readonly LocalizationKey ResendInvitation = new LocalizationKey("Resend invitation again", "重新发送邀请");

        public static readonly LocalizationKey ResetPassword = new LocalizationKey("Reset Password", "重置密码");

        public static readonly LocalizationKey ResetPasswordConfirmation =
            new LocalizationKey("Reset password confirmation", "重置密码确认");

        public static readonly LocalizationKey ResetPasswordInstruction =
            new LocalizationKey("Please reset your password by clicking here: <a href=\"{0}\">link</a>",
                "请点击此处重置密码：<a href=\"{0}\">链接</a>");

        public static readonly LocalizationKey Save = new LocalizationKey("Save", "保存");
        public static readonly LocalizationKey SecondPlayer = new LocalizationKey("Second player", "选手二");

        public static readonly LocalizationKey SecondPlayerEmailIndex =
            new LocalizationKey("Index of Second Player Email column", "选手二邮箱列号码");

        public static readonly LocalizationKey SecondPlayerScore = new LocalizationKey("Second player score", "选手二得分");

        public static readonly LocalizationKey MatchName = new LocalizationKey("Match Name", "赛名");
        public static readonly LocalizationKey SecondPlayerScoreIndex =
            new LocalizationKey("Index of Second Player Score column", "选手二得分列号码");

        public static readonly LocalizationKey SelectOne = new LocalizationKey("Please select one", "请选择");
        public static readonly LocalizationKey SetPassword = new LocalizationKey("Set Password", "设置密码");
        public static readonly LocalizationKey Status = new LocalizationKey("Status", "状态");
        public static readonly LocalizationKey Submit = new LocalizationKey("Submit", "提交");
        public static readonly LocalizationKey Support = new LocalizationKey("Support", "支持");
        public static readonly LocalizationKey HowItWorks = new LocalizationKey("How It Works", "规则说明");

        public static readonly LocalizationKey ThankYouForConfirm =
            new LocalizationKey("Thank you for confirming your email", "感谢您确认邮箱");

        public static readonly LocalizationKey ToggleNavigation = new LocalizationKey("Toggle navigation", "回到主页");

        public static readonly LocalizationKey Unblock = new LocalizationKey("Unblock", "解封");

        public static readonly LocalizationKey UnsuccessfulLoginWithService =
            new LocalizationKey("Unsuccessful login with service", "通过服务登录失败");

        public static readonly LocalizationKey UseAnotherService = new LocalizationKey("Use another service to log in", "使用其他服务登录");

        public static readonly LocalizationKey UseLocalAccountToLogin =
            new LocalizationKey("Use a local account to log in", "使用本地帐号登录");

        public static readonly LocalizationKey WinRate = new LocalizationKey("Win rate", "胜率");
        public static readonly LocalizationKey WinStreak = new LocalizationKey("Win streak", "连胜");
        
        // Player page strings
        public static readonly LocalizationKey PlayerInformation = new LocalizationKey("Player Information", "选手信息");
        public static readonly LocalizationKey Position = new LocalizationKey("Position", "排名");
        public static readonly LocalizationKey Residence = new LocalizationKey("Residence", "居住地");
        public static readonly LocalizationKey CurrentRanking = new LocalizationKey("Current Ranking", "当前段位");
        public static readonly LocalizationKey SaveChanges = new LocalizationKey("Save Changes", "保存修改");
        public static readonly LocalizationKey Cancel = new LocalizationKey("Cancel", "取消");
        public static readonly LocalizationKey EditRankingHistory = new LocalizationKey("Edit Ranking History", "编辑段位历史");
        public static readonly LocalizationKey AddRanking = new LocalizationKey("Add Ranking", "添加段位");
        public static readonly LocalizationKey Organization = new LocalizationKey("Organization", "机构");
        public static readonly LocalizationKey Note = new LocalizationKey("Note", "备注");
        public static readonly LocalizationKey MonthlyRatingHistory = new LocalizationKey("Monthly Rating History", "月等级分历史");
        public static readonly LocalizationKey Month = new LocalizationKey("Month", "月份");
        public static readonly LocalizationKey PreEntry = new LocalizationKey("Pre-entry", "入榜前");
        public static readonly LocalizationKey GameRecords = new LocalizationKey("Game Records", "对局记录");
        public static readonly LocalizationKey Opponent = new LocalizationKey("Opponent", "对手");
        public static readonly LocalizationKey Result = new LocalizationKey("Result", "结果");
        public static readonly LocalizationKey Win = new LocalizationKey("Win", "胜");
        public static readonly LocalizationKey Loss = new LocalizationKey("Loss", "负");
        public static readonly LocalizationKey Draw = new LocalizationKey("Draw", "和");
        public static readonly LocalizationKey SWATournamentsOnly = new LocalizationKey("SWA Tournaments Only", "仅新加坡围棋协会赛事");
        public static readonly LocalizationKey Tournament = new LocalizationKey("Tournament", "赛事");
        public static readonly LocalizationKey Tournaments = new LocalizationKey("Tournaments", "赛事");
        public static readonly LocalizationKey Round = new LocalizationKey("Round", "轮次");
        public static readonly LocalizationKey BackToRating = new LocalizationKey("Back to Rating", "返回等级分");
        public static readonly LocalizationKey RatingHistoryChart = new LocalizationKey("Rating History Chart", "等级分历史图表");
        public static readonly LocalizationKey PhotoUrl = new LocalizationKey("Photo URL", "照片链接");
        public static readonly LocalizationKey UploadPhoto = new LocalizationKey("Upload Photo", "上传照片");
        public static readonly LocalizationKey UploadFile = new LocalizationKey("Upload File", "上传文件");
        public static readonly LocalizationKey SupportedFormats = new LocalizationKey("Supported Formats", "支持的格式");
        public static readonly LocalizationKey Upload = new LocalizationKey("Upload", "上传");
        public static readonly LocalizationKey Screenshot = new LocalizationKey("Screenshot", "截图");
        public static readonly LocalizationKey NotRated = new LocalizationKey("Not Rated", "不计分");
        public static readonly LocalizationKey Prev = new LocalizationKey("Prev", "上一位");
        public static readonly LocalizationKey Next = new LocalizationKey("Next", "下一位");
        public static readonly LocalizationKey PreviousPlayer = new LocalizationKey("Previous Player", "上一位选手");
        public static readonly LocalizationKey NextPlayer = new LocalizationKey("Next Player", "下一位选手");
        public static readonly LocalizationKey NonLocalPlayers = new LocalizationKey("Overseas Players", "海外选手");
        public static readonly LocalizationKey InactivePlayers = new LocalizationKey("Inactive Players", "不活跃选手");
        public static readonly LocalizationKey Statistics = new LocalizationKey("Statistics", "统计");
        public static readonly LocalizationKey Overall = new LocalizationKey("Overall", "总计");
        public static readonly LocalizationKey ThisYear = new LocalizationKey("This Year", "今年");
        public static readonly LocalizationKey LastYear = new LocalizationKey("Last Year", "去年");
        public static readonly LocalizationKey Games = new LocalizationKey("Games", "对局");
        public static readonly LocalizationKey Wins = new LocalizationKey("Wins", "胜");
        public static readonly LocalizationKey Losses = new LocalizationKey("Losses", "负");
        public static readonly LocalizationKey New = new LocalizationKey("NEW", "进榜");
        public static readonly LocalizationKey Returning = new LocalizationKey("RET", "复归");
        public static readonly LocalizationKey Go = new LocalizationKey("Go", "查看");
        public static readonly LocalizationKey Today = new LocalizationKey("Today", "今天");
        public static readonly LocalizationKey MatchesBeforeNotIncluded = new LocalizationKey(
            "Note: Matches before 01/01/2023 (shown in gray) are not included in rating calculations.",
            "注：2023年1月1日之前的比赛（灰色显示）不计入等级分计算。");

        public static readonly LocalizationKey YouCanInviteNewPlayer =
            new LocalizationKey("You can invite new player to the league", "您可以邀请新选手加入联赛");

        public static readonly LocalizationKey YouDontHaveLocalAccount =
            new LocalizationKey(
                "You do not have a local username/password for this site. Add a local account so you can log in without an external login",
                "您在此网站没有本地用户名/密码。添加本地帐号以便无需外部登录即可登录");

        public static readonly LocalizationKey YourContactDetails =
            new LocalizationKey("Your contact details", "您的联系方式");

        public static readonly LocalizationKey YourMessageIsSent =
            new LocalizationKey("Your message has been sent", "您的消息已发送");

        public static readonly LocalizationKey YourPasswordHasBeenReset =
            new LocalizationKey("Your password has been reset", "您的密码已重置");

        public static readonly LocalizationKey YouSuccessfullyAuthenticatedWith =
            new LocalizationKey("You've successfully authenticated with", "您已成功通过以下方式认证");

        // Tournament-related strings
        public static readonly LocalizationKey BackToTournaments = new LocalizationKey("Back to Tournaments", "返回赛事列表");
        public static readonly LocalizationKey TournamentName = new LocalizationKey("Tournament Name", "赛事名称");
        public static readonly LocalizationKey Ordinal = new LocalizationKey("Ordinal", "届次");
        public static readonly LocalizationKey Group = new LocalizationKey("Group", "组别");
        public static readonly LocalizationKey Type = new LocalizationKey("Type", "类型");
        public static readonly LocalizationKey Organizer = new LocalizationKey("Organizer", "主办方");
        public static readonly LocalizationKey Location = new LocalizationKey("Location", "地点");
        public static readonly LocalizationKey Dates = new LocalizationKey("Dates", "日期");
        public static readonly LocalizationKey Notes = new LocalizationKey("Notes", "备注");
        public static readonly LocalizationKey ExternalLinks = new LocalizationKey("External Links", "外部链接");
        public static readonly LocalizationKey Standings = new LocalizationKey("Standings", "积分榜");
        public static readonly LocalizationKey TeamStandings = new LocalizationKey("Team Standings", "团体积分榜");
        public static readonly LocalizationKey Pos = new LocalizationKey("Pos", "名次");
        public static readonly LocalizationKey Player = new LocalizationKey("Player", "选手");
        public static readonly LocalizationKey NbW = new LocalizationKey("NbW", "胜场");
        public static readonly LocalizationKey SOS = new LocalizationKey("SOS", "对手分");
        public static readonly LocalizationKey SOSOS = new LocalizationKey("SOSOS", "对手对手分");
        public static readonly LocalizationKey Promo = new LocalizationKey("Promo", "晋升");
        public static readonly LocalizationKey Before = new LocalizationKey("Before", "赛前");
        public static readonly LocalizationKey After = new LocalizationKey("After", "赛后");
        public static readonly LocalizationKey ViewOriginal = new LocalizationKey("View Original", "查看原版");
        public static readonly LocalizationKey Score = new LocalizationKey("Score", "比分");
        public static readonly LocalizationKey Add = new LocalizationKey("Add", "添加");
        public static readonly LocalizationKey Team = new LocalizationKey("Team", "队伍");
        public static readonly LocalizationKey PlayerPos = new LocalizationKey("Player Pos", "个人名次");
        public static readonly LocalizationKey SumPos = new LocalizationKey("Sum Pos", "名次总和");
        public static readonly LocalizationKey TotalWins = new LocalizationKey("Total Wins", "总胜场");
        public static readonly LocalizationKey Calculate = new LocalizationKey("Calculate", "计算");
        public static readonly LocalizationKey CalculatePositions = new LocalizationKey("Calculate Positions", "计算名次");
        public static readonly LocalizationKey TournamentInformation = new LocalizationKey("Tournament Information", "赛事信息");
        public static readonly LocalizationKey PersonalAward = new LocalizationKey("Personal Award", "个人奖项");
        public static readonly LocalizationKey TeamAward = new LocalizationKey("Team Award", "团体奖项");
        public static readonly LocalizationKey FemaleAward = new LocalizationKey("Female Award", "女子奖项");
        public static readonly LocalizationKey StandingsPhoto = new LocalizationKey("Standings Photo", "积分榜照片");
        public static readonly LocalizationKey Links = new LocalizationKey("Links", "链接");
        public static readonly LocalizationKey Start = new LocalizationKey("Start", "开始");
        public static readonly LocalizationKey End = new LocalizationKey("End", "结束");
        public static readonly LocalizationKey SelectMatches = new LocalizationKey("Select Matches", "选择比赛");
        public static readonly LocalizationKey AddSelected = new LocalizationKey("Add Selected", "添加选中");
        public static readonly LocalizationKey SaveRounds = new LocalizationKey("Save Rounds", "保存轮次");
        public static readonly LocalizationKey ShiftHours = new LocalizationKey("Shift Hours", "调整时间");
        public static readonly LocalizationKey AddPlayer = new LocalizationKey("Add Player", "添加选手");
        public static readonly LocalizationKey BackToEdit = new LocalizationKey("Back to Edit", "返回编辑");
        public static readonly LocalizationKey SelectAll = new LocalizationKey("Select All", "全选");
        public static readonly LocalizationKey SetRoundForSelected = new LocalizationKey("Set Round for selected", "为选中项设置轮次");
        public static readonly LocalizationKey InTournament = new LocalizationKey("In Tournament", "已添加");
        public static readonly LocalizationKey InAnother = new LocalizationKey("In Another", "在其他赛事中");
        public static readonly LocalizationKey AddSelectedMatches = new LocalizationKey("Add Selected Matches", "添加选中的对局");
        public static readonly LocalizationKey ShiftSelectedBy = new LocalizationKey("Shift selected by", "调整选中项");
        public static readonly LocalizationKey Hours = new LocalizationKey("hours", "小时");
        public static readonly LocalizationKey MatchSelectionHelp = new LocalizationKey("Check matches and set round numbers, then click Add. Use 'Save Rounds' to update rounds for existing matches.", "勾选对局并设置轮次，然后点击添加。使用保存轮次来更新已有对局的轮次。");
        public static readonly LocalizationKey NoMatchesFound = new LocalizationKey("No matches found for the selected month.", "所选月份没有找到对局。");
        public static readonly LocalizationKey Filter = new LocalizationKey("Filter", "筛选");
        public static readonly LocalizationKey Apply = new LocalizationKey("Apply", "应用");
        public static readonly LocalizationKey TPos = new LocalizationKey("T.Pos", "团体名次");
        public static readonly LocalizationKey Female = new LocalizationKey("Female", "女子");
        public static readonly LocalizationKey CreateTournament = new LocalizationKey("Create Tournament", "创建赛事");
        public static readonly LocalizationKey EditTournament = new LocalizationKey("Edit Tournament", "编辑赛事");
        public static readonly LocalizationKey DeleteTournament = new LocalizationKey("Delete Tournament", "删除赛事");
        public static readonly LocalizationKey MatchesNotRatedNote = new LocalizationKey(
            "Note: Games with Factor = 0 won't affect ratings (e.g., opponent no-show, handicapped games).",
            "注：权值为0的对局不计入等级分（例如：对手未到场、让子棋等）。");
        public static readonly LocalizationKey NumberOfWins = new LocalizationKey("Number of Wins", "胜场数");
        public static readonly LocalizationKey TeamSOS = new LocalizationKey("Team SOS", "团体对手分");
        public static readonly LocalizationKey TeamSOSOS = new LocalizationKey("Team SOSOS", "团体对手对手分");
        public static readonly LocalizationKey TotalWinsOfPlayers = new LocalizationKey("Total Wins of Players", "选手总胜场");
        public static readonly LocalizationKey MainPlayerBonus = new LocalizationKey("Main Player Bonus", "主将加分");
        public static readonly LocalizationKey SumOfPlayerPositions = new LocalizationKey("Sum of Player Positions", "选手名次总和");
        public static readonly LocalizationKey SwissSystemTooltip = new LocalizationKey("Swiss-system: Undefeated first, then wins → SOS → SOSOS", "瑞士制：全胜优先，然后按胜场→对手分→对手对手分");
        public static readonly LocalizationKey Undefeated = new LocalizationKey("Undefeated", "全胜");
        public static readonly LocalizationKey FemaleChampion = new LocalizationKey("Female Champion", "女子冠军");
        public static readonly LocalizationKey Championships = new LocalizationKey("Championships", "冠军次数");
        public static readonly LocalizationKey TeamChampionships = new LocalizationKey("Team Championships", "团体冠军次数");
        public static readonly LocalizationKey FemaleChampionships = new LocalizationKey("Female Championships", "女子冠军次数");
        public static readonly LocalizationKey Photo = new LocalizationKey("Photo", "照片");
        public static readonly LocalizationKey AddPhoto = new LocalizationKey("Add Photo", "添加照片");
        public static readonly LocalizationKey EditPhoto = new LocalizationKey("Edit Photo", "编辑照片");
        public static readonly LocalizationKey MatchPhoto = new LocalizationKey("Match Photo", "对局照片");
        public static readonly LocalizationKey ResultPhoto = new LocalizationKey("Result Photo", "结果照片");
        public static readonly LocalizationKey GameRecordLink = new LocalizationKey("Game Record", "棋谱");
        public static readonly LocalizationKey AddGameRecord = new LocalizationKey("Add Game Record", "添加棋谱");
        public static readonly LocalizationKey UrlOrUpload = new LocalizationKey("Enter URL or upload file", "输入链接或上传文件");
        public static readonly LocalizationKey Close = new LocalizationKey("Close", "关闭");
        public static readonly LocalizationKey DeletePlayer = new LocalizationKey("Delete Player", "删除选手");
        public static readonly LocalizationKey DeletePlayerConfirm = new LocalizationKey("Are you sure you want to delete this player? This will permanently delete all their matches and tournament records.", "确定要删除此选手吗？这将永久删除其所有对局和赛事记录。");
        public static readonly LocalizationKey PlayerDeleted = new LocalizationKey("Player and all related data deleted successfully.", "选手及所有相关数据已成功删除。");
        public static readonly LocalizationKey MatchesToDelete = new LocalizationKey("Matches to be deleted", "将被删除的对局");
        public static readonly LocalizationKey TournamentsAffected = new LocalizationKey("Tournaments affected", "受影响的赛事");
        public static readonly LocalizationKey Title = new LocalizationKey("Title", "头衔");
        public static readonly LocalizationKey FormerTitle = new LocalizationKey("Former Title", "曾获头衔");
        public static readonly LocalizationKey Promotions = new LocalizationKey("Promotions", "升段");
        public static readonly LocalizationKey ActivePlayers = new LocalizationKey("Active Players", "活跃选手");

        private const string En = "en";
        private const string Cn = "cn";

        private readonly Dictionary<string, string> _values;

        private LocalizationKey(string en, string cn = null)
        {
            _values = new Dictionary<string, string>
            {
                {En, en},
                {Cn, cn ?? en}
            };
        }


        public static string GetLocalization(string name, string preferedLanguage)
        {
            var property = typeof (LocalizationKey).GetField(name,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);

            if (property == null)
            {
                return name;
            }

            var localization = property.GetValue(null) as LocalizationKey;

            if (localization == null)
            {
                return name;
            }

            if (localization._values.ContainsKey(preferedLanguage) &&
                !string.IsNullOrEmpty(localization._values[preferedLanguage]))
            {
                return localization._values[preferedLanguage];
            }

            return localization._values.FirstOrDefault(kvp => !string.IsNullOrEmpty(kvp.Value)).Value;
        }
    }
}
