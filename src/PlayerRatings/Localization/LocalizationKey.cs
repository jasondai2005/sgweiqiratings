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
            new LocalizationKey("Add another service to log in", "Добавить привязку к внешнему сервису", "添加使用其他服务的登录方式");

        public static readonly LocalizationKey AddNewResult = new LocalizationKey("Add new result", "Добавить результат", "记录比赛结果");

        public static readonly LocalizationKey AddThisAndAnotherOne = new LocalizationKey("Add this and another one",
            "Добавить этот и еще один", "添加并继续");

        public static readonly LocalizationKey AddThisAndGoToRating = new LocalizationKey("Add and go to the rating",
            "Добавить и посмотреть рейтинг", "添加并查看等级分");

        public static readonly LocalizationKey AgainstFor = new LocalizationKey("Goals Against / For",
            "Голы Забито / Пропущено", "得分/丢分");

        public static readonly LocalizationKey AreYouSureDelete =
            new LocalizationKey("Are you sure you want to delete this", "Вы уверены, что хотите удалить", "请确认删除");

        public static readonly LocalizationKey AssociateForm = new LocalizationKey("Association Form",
            "Связать аккаунты", "关联表格");

        public static readonly LocalizationKey AssociateYourAccount = new LocalizationKey("Associate your {0} account",
            "Привязать ваш аккаунт: {0}", "关联您的{0}帐号");

        public static readonly LocalizationKey BackToList = new LocalizationKey("Back to list", "Назад", "返回列表");
        public static readonly LocalizationKey Block = new LocalizationKey("Block", "Заблокировать", "阻止");
        public static readonly LocalizationKey ChangePassword = new LocalizationKey("Change Password", "Изменить пароль", "修改密码");

        public static readonly LocalizationKey ChangePasswordForm = new LocalizationKey("Change Password Form",
            "Форма изменения пароля", "修改密码表格");

        public static readonly LocalizationKey ChangeYourAccountSettings =
            new LocalizationKey("Change your account settings", "Изменить настройки аккаунта", "修改帐号设置");

        public static readonly LocalizationKey CheckEmailToReset =
            new LocalizationKey("Please check your email to reset your password",
                "Проверьте ваш почтовый ящик. Мы отправили вам ссылку для восстановления", "请查看邮件以修改密码");

        public static readonly LocalizationKey ClickHereToLogin = new LocalizationKey("Click here to Log in",
            "Кликнете здесь чтобы залогиниться", "点击登陆");

        public static readonly LocalizationKey ConfirmAccount =
            new LocalizationKey("Please confirm your account by clicking this link: <a href=\"{0}\">{0}</a>",
                "Пожалуйста, подтвердите свой аккаунт: <a href=\"{0}\">{0}</a>", "请点击链接<a href=\"{0}\">{0}</a>确认帐号");

        public static readonly LocalizationKey ConfirmEmail = new LocalizationKey("Confirm Email",
            "Подтверждение адреса", "确认邮件");

        public static readonly LocalizationKey ConfirmPassword = new LocalizationKey("Confirm password",
            "Подтверждение пароля", "确认密码");

        public static readonly LocalizationKey Create = new LocalizationKey("Create", "Добавить", "创建");
        public static readonly LocalizationKey CreateNew = new LocalizationKey("Create New", "Добавить", "新建");

        public static readonly LocalizationKey CreateNewAccount = new LocalizationKey("Create a new account",
            "Создание нового аккаунта", "创建新帐号");

        public static readonly LocalizationKey CreateYourLeague = new LocalizationKey("Create your league",
            "Создай свою лигу", "创建您的联赛");

        public static readonly LocalizationKey Date = new LocalizationKey("Date", "Дата", "日期");

        public static readonly LocalizationKey DateIndex = new LocalizationKey("Index of Date column",
            "Номер колонки с датой", "日期列号码");

        public static readonly LocalizationKey DateTimeFormat = new LocalizationKey("Date time format",
            "Формат даты и времени", "日期时间格式");

        public static readonly LocalizationKey Delete = new LocalizationKey("Delete", "Удалить", "删除");
        public static readonly LocalizationKey Details = new LocalizationKey("Details", "Подробности", "详细信息");

        public static readonly LocalizationKey DisplayName = new LocalizationKey("Display name", "Имя", "名字");
        public static readonly LocalizationKey Username = new LocalizationKey("Username", "Логин", "用户名");
        public static readonly LocalizationKey BirthYear = new LocalizationKey("Birth Year", "Birth Year", "生年");
        public static readonly LocalizationKey Ranking = new LocalizationKey("Ranking", "Ranking", "段/级位");
        public static readonly LocalizationKey RankedDate = new LocalizationKey("Ranked Date", "Ranked Date", "升段/级时间");
        public static readonly LocalizationKey OriRanking = new LocalizationKey("Original Ranking", "Original Ranking", "原段位");
        public static readonly LocalizationKey Edit = new LocalizationKey("Edit", "Редактировать", "编辑");
        public static readonly LocalizationKey Elo = new LocalizationKey("Elo", "Elo", "等级分");
        public static readonly LocalizationKey Email = new LocalizationKey("Email", "Email", "邮箱");
        public static readonly LocalizationKey ShiftRating = new LocalizationKey("Shift Rating", "Shift Rating", "等级分值");
        public static readonly LocalizationKey RankingHistory = new LocalizationKey("Ranking History", "Ranking History", "升段/级纪录");

        public static readonly LocalizationKey EnterYourEmail = new LocalizationKey("Enter your email",
            "Введите ваш емеил", "请输入您的邮箱");

        public static readonly LocalizationKey Error = new LocalizationKey("Error", "Ошибка", "错误");

        public static readonly LocalizationKey ErrorOccurred = new LocalizationKey("An error has occurred",
            "Возникла ошибка", "有错误发生");

        public static readonly LocalizationKey ErrorOccurredWhileProcessing =
            new LocalizationKey("An error occurred while processing your request",
                "Возникла ошибка во время обработки вашего запроса", "处理您的请求时出错");

        public static readonly LocalizationKey ExternalLoginAdded = new LocalizationKey("The external login was added",
            "Логин добавлен", "已添加外部登录方式");

        public static readonly LocalizationKey ExternalLoginRemoved =
            new LocalizationKey("The external login was removed", "Логин удален", "已删除外部登录方式");

        public static readonly LocalizationKey ExternalLogins = new LocalizationKey("External Logins", "Внешние сервисы", "外部登录");

        public static readonly LocalizationKey ExternalRegisterSuccessInstuction =
            new LocalizationKey(
                "Please enter a name for this site below and click the Register button to finish logging in",
                "Пожалуйста, введите ваше имя и нажмите кнопку Зарегистрироваться");

        public static readonly LocalizationKey FactorIndex = new LocalizationKey("Index of Factor column",
            "Номер колонки с фактором", "仅值列号码");

        public static readonly LocalizationKey Factor = new LocalizationKey("Factor",
            "Factor", "权值");

        public static readonly LocalizationKey File = new LocalizationKey("File", "Файл", "文件");
        public static readonly LocalizationKey FirstPlayer = new LocalizationKey("First player", "Первый игрок", "选手一");

        public static readonly LocalizationKey FirstPlayerEmailIndex =
            new LocalizationKey("Index of First Player Email column", "Номер колонки с email первого игрока", "选手一邮箱列号码");

        public static readonly LocalizationKey FirstPlayerScore = new LocalizationKey("First player score",
            "Счет первого игрока", "选手一得分");

        public static readonly LocalizationKey FirstPlayerScoreIndex =
            new LocalizationKey("Index of First Player Score column", "Номер колонки со счетом первого игрока", "选手一得分列号码");

        public static readonly LocalizationKey Forecast = new LocalizationKey("Forecast", "Предсказание", "预报");

        public static readonly LocalizationKey ForgotPasswordConfirmation =
            new LocalizationKey("Forgot Password Confirmation", "Подтверждение");

        public static readonly LocalizationKey ForgotYourPassword = new LocalizationKey("Forgot your password",
            "Забыли ваш пароль");

        public static readonly LocalizationKey HasNoConfiguredServices =
            new LocalizationKey("Authentication services was not configured", "Нету настроенных сервисов");

        public static readonly LocalizationKey Hello = new LocalizationKey("Hello", "Привет", "您好");

        public static readonly LocalizationKey Import = new LocalizationKey("Import", "Импортировать", "导入");

        public static readonly LocalizationKey ImportData =
            new LocalizationKey(
                "You can import your matches in csv format. Each record must contain Date, First Player Email, Second Player Email, First Player Score, Second Player Score and optionally Factor. Unknown users will be invited to league automatically",
                "Вы можете импортировать ваши матчи в csv формате. Каждая запись должна содержать дату, email первого игрока, email второго игрока, счет первого игрока, счет второго игрока и опционально фактор. Неизвестные игроки будут автоматически приглашены в лигу",
                "您可以导入csv格式的比赛数据。每条数据必须包含以下各列：日期、选手一邮箱、选手二邮箱、选手一得分、选手二得分，还可包含一个可选列因子。未知选手将被自动邀请进联赛。");

        public static readonly LocalizationKey ImportMatches = new LocalizationKey("Import matches",
            "Импортировать матчи", "导入比赛数据");

        public static readonly LocalizationKey InvitationForm = new LocalizationKey("Invintation Form",
            "Форма приглашения", "邀请选手");

        public static readonly LocalizationKey Invite = new LocalizationKey("Invite", "Пригласить", "邀请");

        public static readonly LocalizationKey InvitedYou =
            new LocalizationKey("{0} invited you to join the rating system",
                "{0} пригласил Вас присоединиться к рейтингу");

        public static readonly LocalizationKey InviteNew =
            new LocalizationKey("Invite new player", "Пригласить нового пользователя", "邀请新选手");

        public static readonly LocalizationKey Invites = new LocalizationKey("Invites", "Приглашения", "邀请");
        public static readonly LocalizationKey LastMatches = new LocalizationKey("Last matches", "Последние матчи", "最新赛事");
        public static readonly LocalizationKey League = new LocalizationKey("League", "Лига", "联赛");

        public static readonly LocalizationKey LeagueNotFound =
            new LocalizationKey("Can not find league or you don't have access",
                "Не могу найти лигу или у вас нет доступа");

        public static readonly LocalizationKey Leagues = new LocalizationKey("Leagues", "Лиги", "联赛");
        public static readonly LocalizationKey LockedOut = new LocalizationKey("Locked out", "Заблокировано", "锁出");

        public static readonly LocalizationKey LockedOutTryLater =
            new LocalizationKey("This account has been locked out, please try again later",
                "Этот аккаунт заблокирован. Попробуйте позднее");

        public static readonly LocalizationKey LogIn = new LocalizationKey("Log in", "Войти", "登录");
        public static readonly LocalizationKey LoginFailure = new LocalizationKey("Login Failure", "Неудачный логин", "登录失败");

        public static readonly LocalizationKey LogInUsingExternal = new LocalizationKey(
            "Log in using your {0} account", "Войти используя ваш {0} аккаунт");

        public static readonly LocalizationKey LogOff = new LocalizationKey("Log off", "Выйти", "登出");
        public static readonly LocalizationKey LooseStreak = new LocalizationKey("Lose streak", "Поражения подряд", "连败");
        public static readonly LocalizationKey Manage = new LocalizationKey("Manage", "Управлять", "管理");

        public static readonly LocalizationKey ManageYourAccount = new LocalizationKey("Manage your account",
            "Управление аккаунтом");

        public static readonly LocalizationKey ManageYourExternalLogins =
            new LocalizationKey("Manage your external logins", "Управление внешними аккаунтами");

        public static readonly LocalizationKey Match = new LocalizationKey("Match", "Матч", "比赛");
        public static readonly LocalizationKey Matches = new LocalizationKey("Matches", "Матчи", "比赛");
        public static readonly LocalizationKey Message = new LocalizationKey("Message", "Сообщение");
        public static readonly LocalizationKey NewPassword = new LocalizationKey("New password", "Новый пароль");

        public static readonly LocalizationKey NoLeagues =
            new LocalizationKey("You have no leagues", "У вас нету ни одной лиги");

        public static readonly LocalizationKey OldPassword = new LocalizationKey("Old password", "Старый пароль");

        public static readonly LocalizationKey Password = new LocalizationKey("Password", "Пароль");

        public static readonly LocalizationKey PasswordChanged = new LocalizationKey("Your password has been changed",
            "Ваш пароль изменен");

        public static readonly LocalizationKey PasswordSet = new LocalizationKey("Your password has been set",
            "Пароль установлен");

        public static readonly LocalizationKey PlayerNotFound =
            new LocalizationKey("Can not find player or you don't have access",
                "Не могу найти игрока или у вас нет доступа");

        public static readonly LocalizationKey Players =
            new LocalizationKey("Players", "Игроки", "选手");

        public static readonly LocalizationKey Rating = new LocalizationKey("Rating", "Рейтинг", "等级分");
        public static readonly LocalizationKey ShowSinRankingsOnly = new LocalizationKey("Only show Singapore rankings", "", "仅显示新加坡段、级位");
        public static readonly LocalizationKey ShowAllRankings = new LocalizationKey("Show all rankings", "", "显示所有段、级位");
        public static readonly LocalizationKey ProtectedRatingsSupported = new LocalizationKey("Protected ratings option is on", "", "等级分保护已开启");
        public static readonly LocalizationKey ProtectedRatingsNotSupported = new LocalizationKey("Protected ratings option is off", "", "等级分保护未开启");
        public static readonly LocalizationKey HistoryRating = new LocalizationKey("History Rating", "History Rating", "历史等级分");

        public static readonly LocalizationKey RatingSource = new LocalizationKey("Source of rating",
            "Источник рейтинга", "等级分来源");

        public static readonly LocalizationKey Register = new LocalizationKey("Register", "Зарегистрироваться", "注册");

        public static readonly LocalizationKey RegisteredLogins = new LocalizationKey("Registered Logins",
            "Добавленные аккаунты");

        public static readonly LocalizationKey RegisterNewUser = new LocalizationKey("Register as a new user",
            "Зарегистрировать нового пользователя");

        public static readonly LocalizationKey RememberMe = new LocalizationKey("Remember me", "Запомнить меня", "记住我");
        public static readonly LocalizationKey Remove = new LocalizationKey("Remove", "Удалить", "移除");

        public static readonly LocalizationKey RemoveExternalFrom =
            new LocalizationKey("Remove this {0} login from your account", "Удалить привязку к {0}");

        public static readonly LocalizationKey ResendInvitation = new LocalizationKey("Resend invitation again",
            "Отправить приглашение еще раз");

        public static readonly LocalizationKey ResetPassword = new LocalizationKey("Reset Password",
            "Восстановить пароль");

        public static readonly LocalizationKey ResetPasswordConfirmation =
            new LocalizationKey("Reset password confirmation", "Подтверждение сброса пароля");

        public static readonly LocalizationKey ResetPasswordInstruction =
            new LocalizationKey("Please reset your password by clicking here: <a href=\"{0}\">link</a>",
                "Чтобы сбросить пароль передйдите по <a href=\"{0}\">ссылке</a>");

        public static readonly LocalizationKey Save = new LocalizationKey("Save", "Сохранить", "保存");
        public static readonly LocalizationKey SecondPlayer = new LocalizationKey("Second player", "Второй игрок", "选手二");

        public static readonly LocalizationKey SecondPlayerEmailIndex =
            new LocalizationKey("Index of Second Player Email column", "Номер колонки с email второго игрока", "选手二邮箱列号码");

        public static readonly LocalizationKey SecondPlayerScore = new LocalizationKey("Second player score",
            "Счет второго игрока", "选手二得分");

        public static readonly LocalizationKey MatchName = new LocalizationKey("Match Name",
            "Match Name", "赛名");
        public static readonly LocalizationKey SecondPlayerScoreIndex =
            new LocalizationKey("Index of Second Player Score column", "Номер колонки со счетом второго игрока", "选手二得分列号码");

        public static readonly LocalizationKey SelectOne = new LocalizationKey("Please select one", "Выберите игрока", "请选择");
        public static readonly LocalizationKey SetPassword = new LocalizationKey("Set Password", "Установить пароль", "设置密码");
        public static readonly LocalizationKey Status = new LocalizationKey("Status", "Статус", "状态");
        public static readonly LocalizationKey Submit = new LocalizationKey("Submit", "Отправить", "提交");
        public static readonly LocalizationKey Support = new LocalizationKey("Support", "Поддержка", "支持");

        public static readonly LocalizationKey ThankYouForConfirm =
            new LocalizationKey("Thank you for confirming your email", "Спасибо за подтверждение вашего адреса");

        public static readonly LocalizationKey ToggleNavigation = new LocalizationKey("Toggle navigation",
            "Переключение навигации", "回到主页");

        public static readonly LocalizationKey Unblock = new LocalizationKey("Unblock", "Разблокировать", "解封");

        public static readonly LocalizationKey UnsuccessfulLoginWithService =
            new LocalizationKey("Unsuccessful login with service", "Не получилось войти через сервис");

        public static readonly LocalizationKey UseAnotherService = new LocalizationKey("Use another service to log in",
            "Использовать сторонние сервисы");

        public static readonly LocalizationKey UseLocalAccountToLogin =
            new LocalizationKey("Use a local account to log in", "Использовать аккаунт для входа");

        public static readonly LocalizationKey WinRate = new LocalizationKey("Win rate", "Соотношение побед", "胜率");
        public static readonly LocalizationKey WinStreak = new LocalizationKey("Win streak", "Победы подряд", "连胜");
        
        // Player page strings
        public static readonly LocalizationKey PlayerInformation = new LocalizationKey("Player Information", "Информация", "选手信息");
        public static readonly LocalizationKey Position = new LocalizationKey("Position", "Позиция", "排名");
        public static readonly LocalizationKey Residence = new LocalizationKey("Residence", "Residence", "居住地");
        public static readonly LocalizationKey CurrentRanking = new LocalizationKey("Current Ranking", "Current Ranking", "当前段位");
        public static readonly LocalizationKey SaveChanges = new LocalizationKey("Save Changes", "Сохранить", "保存修改");
        public static readonly LocalizationKey Cancel = new LocalizationKey("Cancel", "Отмена", "取消");
        public static readonly LocalizationKey EditRankingHistory = new LocalizationKey("Edit Ranking History", "Edit Ranking History", "编辑段位历史");
        public static readonly LocalizationKey AddRanking = new LocalizationKey("Add Ranking", "Add Ranking", "添加段位");
        public static readonly LocalizationKey Organization = new LocalizationKey("Organization", "Organization", "机构");
        public static readonly LocalizationKey Note = new LocalizationKey("Note", "Note", "备注");
        public static readonly LocalizationKey MonthlyRatingHistory = new LocalizationKey("Monthly Rating History", "Monthly Rating History", "月等级分历史");
        public static readonly LocalizationKey Month = new LocalizationKey("Month", "Month", "月份");
        public static readonly LocalizationKey PreEntry = new LocalizationKey("Pre-entry", "Pre-entry", "入榜前");
        public static readonly LocalizationKey GameRecords = new LocalizationKey("Game Records", "Game Records", "对局记录");
        public static readonly LocalizationKey Opponent = new LocalizationKey("Opponent", "Opponent", "对手");
        public static readonly LocalizationKey Result = new LocalizationKey("Result", "Result", "结果");
        public static readonly LocalizationKey Win = new LocalizationKey("Win", "Win", "胜");
        public static readonly LocalizationKey Loss = new LocalizationKey("Loss", "Loss", "负");
        public static readonly LocalizationKey Draw = new LocalizationKey("Draw", "Draw", "和");
        public static readonly LocalizationKey SWATournamentsOnly = new LocalizationKey("SWA Tournaments Only", "SWA Tournaments Only", "仅新加坡围棋协会赛事");
        public static readonly LocalizationKey Tournament = new LocalizationKey("Tournament", "Tournament", "赛事");
        public static readonly LocalizationKey Tournaments = new LocalizationKey("Tournaments", "Tournaments", "赛事");
        public static readonly LocalizationKey Round = new LocalizationKey("Round", "Round", "轮次");
        public static readonly LocalizationKey BackToRating = new LocalizationKey("Back to Rating", "Back to Rating", "返回等级分");
        public static readonly LocalizationKey RatingHistoryChart = new LocalizationKey("Rating History Chart", "Rating History Chart", "等级分历史图表");
        public static readonly LocalizationKey PhotoUrl = new LocalizationKey("Photo URL", "Photo URL", "照片链接");
        public static readonly LocalizationKey UploadPhoto = new LocalizationKey("Upload Photo", "Upload Photo", "上传照片");
        public static readonly LocalizationKey UploadFile = new LocalizationKey("Upload File", "Upload File", "上传文件");
        public static readonly LocalizationKey SupportedFormats = new LocalizationKey("Supported Formats", "Supported Formats", "支持的格式");
        public static readonly LocalizationKey Upload = new LocalizationKey("Upload", "Upload", "上传");
        public static readonly LocalizationKey Screenshot = new LocalizationKey("Screenshot", "Скриншот", "截图");
        public static readonly LocalizationKey NotRated = new LocalizationKey("Not Rated", "Не учитывается", "不计分");
        public static readonly LocalizationKey Prev = new LocalizationKey("Prev", "Пред", "上一位");
        public static readonly LocalizationKey Next = new LocalizationKey("Next", "След", "下一位");
        public static readonly LocalizationKey PreviousPlayer = new LocalizationKey("Previous Player", "Предыдущий игрок", "上一位选手");
        public static readonly LocalizationKey NextPlayer = new LocalizationKey("Next Player", "Следующий игрок", "下一位选手");
        public static readonly LocalizationKey NonLocalPlayers = new LocalizationKey("Overseas Players", "Overseas Players", "海外选手");
        public static readonly LocalizationKey InactivePlayers = new LocalizationKey("Inactive Players", "Inactive Players", "不活跃选手");
        public static readonly LocalizationKey Statistics = new LocalizationKey("Statistics", "Статистика", "统计");
        public static readonly LocalizationKey Overall = new LocalizationKey("Overall", "Всего", "总计");
        public static readonly LocalizationKey ThisYear = new LocalizationKey("This Year", "Этот год", "今年");
        public static readonly LocalizationKey LastYear = new LocalizationKey("Last Year", "Last Year", "去年");
        public static readonly LocalizationKey Games = new LocalizationKey("Games", "Игры", "对局");
        public static readonly LocalizationKey Wins = new LocalizationKey("Wins", "Победы", "胜");
        public static readonly LocalizationKey Losses = new LocalizationKey("Losses", "Поражения", "负");
        public static readonly LocalizationKey New = new LocalizationKey("NEW", "NEW", "进榜");
        public static readonly LocalizationKey Returning = new LocalizationKey("RET", "RET", "复归");
        public static readonly LocalizationKey Go = new LocalizationKey("Go", "Go", "查看");
        public static readonly LocalizationKey Today = new LocalizationKey("Today", "Today", "今天");
        public static readonly LocalizationKey MatchesBeforeNotIncluded = new LocalizationKey(
            "Note: Matches before 01/01/2023 (shown in gray) are not included in rating calculations.",
            "Примечание: матчи до 01/01/2023 (показаны серым) не включены в расчет рейтинга.",
            "注：2023年1月1日之前的比赛（灰色显示）不计入等级分计算。");

        public static readonly LocalizationKey YouCanInviteNewPlayer =
            new LocalizationKey("You can invite new player to the league", "Вы можете добавить нового игрока в лигу", "您可以邀请新选手加入联赛");

        public static readonly LocalizationKey YouDontHaveLocalAccount =
            new LocalizationKey(
                "You do not have a local username/password for this site. Add a local account so you can log in without an external login",
                "У вас нету локального аккаунта в системе. Добавьте аккаунт, чтобы заходить в систему без внешних сервисов");

        public static readonly LocalizationKey YourContactDetails =
            new LocalizationKey("Your contact details", "Ваши контактные данные");

        public static readonly LocalizationKey YourMessageIsSent =
            new LocalizationKey("Your message has been sent", "Ваше сообщение отправлено");

        public static readonly LocalizationKey YourPasswordHasBeenReset =
            new LocalizationKey("Your password has been reset", "Ваш пароль был сброшен");

        public static readonly LocalizationKey YouSuccessfullyAuthenticatedWith =
            new LocalizationKey("You've successfully authenticated with", "Вы успешно зашли через");

        // Tournament-related strings
        public static readonly LocalizationKey BackToTournaments = new LocalizationKey("Back to Tournaments", "Назад к турнирам", "返回赛事列表");
        public static readonly LocalizationKey TournamentName = new LocalizationKey("Tournament Name", "Название турнира", "赛事名称");
        public static readonly LocalizationKey Ordinal = new LocalizationKey("Ordinal", "Порядковый номер", "届次");
        public static readonly LocalizationKey Group = new LocalizationKey("Group", "Группа", "组别");
        public static readonly LocalizationKey Type = new LocalizationKey("Type", "Тип", "类型");
        public static readonly LocalizationKey Organizer = new LocalizationKey("Organizer", "Организатор", "主办方");
        public static readonly LocalizationKey Location = new LocalizationKey("Location", "Место", "地点");
        public static readonly LocalizationKey Dates = new LocalizationKey("Dates", "Даты", "日期");
        public static readonly LocalizationKey Notes = new LocalizationKey("Notes", "Заметки", "备注");
        public static readonly LocalizationKey ExternalLinks = new LocalizationKey("External Links", "Внешние ссылки", "外部链接");
        public static readonly LocalizationKey Standings = new LocalizationKey("Standings", "Турнирная таблица", "积分榜");
        public static readonly LocalizationKey TeamStandings = new LocalizationKey("Team Standings", "Командный зачет", "团体积分榜");
        public static readonly LocalizationKey Pos = new LocalizationKey("Pos", "Поз", "名次");
        public static readonly LocalizationKey Player = new LocalizationKey("Player", "Игрок", "选手");
        public static readonly LocalizationKey NbW = new LocalizationKey("NbW", "Поб", "胜场");
        public static readonly LocalizationKey SOS = new LocalizationKey("SOS", "SOS", "对手分");
        public static readonly LocalizationKey SOSOS = new LocalizationKey("SOSOS", "SOSOS", "对手对手分");
        public static readonly LocalizationKey Promo = new LocalizationKey("Promo", "Повыш", "晋升");
        public static readonly LocalizationKey Before = new LocalizationKey("Before", "До", "赛前");
        public static readonly LocalizationKey After = new LocalizationKey("After", "После", "赛后");
        public static readonly LocalizationKey ViewOriginal = new LocalizationKey("View Original", "Посмотреть оригинал", "查看原版");
        public static readonly LocalizationKey Score = new LocalizationKey("Score", "Счет", "比分");
        public static readonly LocalizationKey Add = new LocalizationKey("Add", "Добавить", "添加");
        public static readonly LocalizationKey Team = new LocalizationKey("Team", "Команда", "队伍");
        public static readonly LocalizationKey PlayerPos = new LocalizationKey("Player Pos", "Личн", "个人名次");
        public static readonly LocalizationKey SumPos = new LocalizationKey("Sum Pos", "Сумма позиций", "名次总和");
        public static readonly LocalizationKey TotalWins = new LocalizationKey("Total Wins", "Всего побед", "总胜场");
        public static readonly LocalizationKey Calculate = new LocalizationKey("Calculate", "Рассчитать", "计算");
        public static readonly LocalizationKey CalculatePositions = new LocalizationKey("Calculate Positions", "Рассчитать позиции", "计算名次");
        public static readonly LocalizationKey TournamentInformation = new LocalizationKey("Tournament Information", "Информация о турнире", "赛事信息");
        public static readonly LocalizationKey PersonalAward = new LocalizationKey("Personal Award", "Личный зачет", "个人奖项");
        public static readonly LocalizationKey TeamAward = new LocalizationKey("Team Award", "Командный зачет", "团体奖项");
        public static readonly LocalizationKey FemaleAward = new LocalizationKey("Female Award", "Женский зачет", "女子奖项");
        public static readonly LocalizationKey StandingsPhoto = new LocalizationKey("Standings Photo", "Фото таблицы", "积分榜照片");
        public static readonly LocalizationKey Links = new LocalizationKey("Links", "Ссылки", "链接");
        public static readonly LocalizationKey Start = new LocalizationKey("Start", "Начало", "开始");
        public static readonly LocalizationKey End = new LocalizationKey("End", "Конец", "结束");
        public static readonly LocalizationKey SelectMatches = new LocalizationKey("Select Matches", "Выбрать матчи", "选择比赛");
        public static readonly LocalizationKey AddSelected = new LocalizationKey("Add Selected", "Добавить выбранные", "添加选中");
        public static readonly LocalizationKey SaveRounds = new LocalizationKey("Save Rounds", "Сохранить раунды", "保存轮次");
        public static readonly LocalizationKey ShiftHours = new LocalizationKey("Shift Hours", "Сдвинуть часы", "调整时间");
        public static readonly LocalizationKey AddPlayer = new LocalizationKey("Add Player", "Добавить игрока", "添加选手");
        public static readonly LocalizationKey BackToEdit = new LocalizationKey("Back to Edit", "Назад к редактированию", "返回编辑");
        public static readonly LocalizationKey SelectAll = new LocalizationKey("Select All", "Выбрать все", "全选");
        public static readonly LocalizationKey SetRoundForSelected = new LocalizationKey("Set Round for selected", "Установить раунд для выбранных", "为选中项设置轮次");
        public static readonly LocalizationKey InTournament = new LocalizationKey("In Tournament", "В турнире", "已添加");
        public static readonly LocalizationKey InAnother = new LocalizationKey("In Another", "В другом", "在其他赛事中");
        public static readonly LocalizationKey AddSelectedMatches = new LocalizationKey("Add Selected Matches", "Добавить выбранные матчи", "添加选中的对局");
        public static readonly LocalizationKey ShiftSelectedBy = new LocalizationKey("Shift selected by", "Сдвинуть выбранные на", "调整选中项");
        public static readonly LocalizationKey Hours = new LocalizationKey("hours", "часов", "小时");
        public static readonly LocalizationKey MatchSelectionHelp = new LocalizationKey("Check matches and set round numbers, then click Add. Use 'Save Rounds' to update rounds for existing matches.", "Отметьте матчи и установите номера раундов, затем нажмите Добавить. Используйте 'Сохранить раунды' для обновления раундов существующих матчей.", "勾选对局并设置轮次，然后点击添加。使用保存轮次来更新已有对局的轮次。");
        public static readonly LocalizationKey NoMatchesFound = new LocalizationKey("No matches found for the selected month.", "Матчи не найдены за выбранный месяц.", "所选月份没有找到对局。");
        public static readonly LocalizationKey Filter = new LocalizationKey("Filter", "Фильтр", "筛选");
        public static readonly LocalizationKey Apply = new LocalizationKey("Apply", "Применить", "应用");
        public static readonly LocalizationKey TPos = new LocalizationKey("T.Pos", "Ком", "团体名次");
        public static readonly LocalizationKey Female = new LocalizationKey("Female", "Жен", "女子");
        public static readonly LocalizationKey CreateTournament = new LocalizationKey("Create Tournament", "Создать турнир", "创建赛事");
        public static readonly LocalizationKey EditTournament = new LocalizationKey("Edit Tournament", "Редактировать турнир", "编辑赛事");
        public static readonly LocalizationKey DeleteTournament = new LocalizationKey("Delete Tournament", "Удалить турнир", "删除赛事");
        public static readonly LocalizationKey MatchesNotRatedNote = new LocalizationKey(
            "Note: Games with Factor = 0 won't affect ratings (e.g., opponent no-show, handicapped games).",
            "Примечание: Игры с Factor = 0 не влияют на рейтинг (напр., неявка соперника, игры с форой).",
            "注：权值为0的对局不计入等级分（例如：对手未到场、让子棋等）。");
        public static readonly LocalizationKey NumberOfWins = new LocalizationKey("Number of Wins", "Количество побед", "胜场数");
        public static readonly LocalizationKey TeamSOS = new LocalizationKey("Team SOS", "Командный SOS", "团体对手分");
        public static readonly LocalizationKey TeamSOSOS = new LocalizationKey("Team SOSOS", "Командный SOSOS", "团体对手对手分");
        public static readonly LocalizationKey TotalWinsOfPlayers = new LocalizationKey("Total Wins of Players", "Общее кол-во побед", "选手总胜场");
        public static readonly LocalizationKey MainPlayerBonus = new LocalizationKey("Main Player Bonus", "Бонус главного игрока", "主将加分");
        public static readonly LocalizationKey SumOfPlayerPositions = new LocalizationKey("Sum of Player Positions", "Сумма позиций игроков", "选手名次总和");
        public static readonly LocalizationKey SwissSystemTooltip = new LocalizationKey("Swiss-system: Undefeated first, then wins → SOS → SOSOS", "Швейцарская система: Без поражений первые, затем победы → SOS → SOSOS", "瑞士制：全胜优先，然后按胜场→对手分→对手对手分");
        public static readonly LocalizationKey Undefeated = new LocalizationKey("Undefeated", "Без поражений", "全胜");
        public static readonly LocalizationKey FemaleChampion = new LocalizationKey("Female Champion", "Женский чемпион", "女子冠军");
        public static readonly LocalizationKey Championships = new LocalizationKey("Championships", "Чемпионства", "冠军次数");
        public static readonly LocalizationKey TeamChampionships = new LocalizationKey("Team Championships", "Командные чемпионства", "团体冠军次数");
        public static readonly LocalizationKey FemaleChampionships = new LocalizationKey("Female Championships", "Женские чемпионства", "女子冠军次数");
        public static readonly LocalizationKey Photo = new LocalizationKey("Photo", "Фото", "照片");
        public static readonly LocalizationKey AddPhoto = new LocalizationKey("Add Photo", "Добавить фото", "添加照片");
        public static readonly LocalizationKey EditPhoto = new LocalizationKey("Edit Photo", "Редактировать фото", "编辑照片");
        public static readonly LocalizationKey MatchPhoto = new LocalizationKey("Match Photo", "Фото матча", "对局照片");
        public static readonly LocalizationKey ResultPhoto = new LocalizationKey("Result Photo", "Фото результата", "结果照片");
        public static readonly LocalizationKey GameRecordLink = new LocalizationKey("Game Record", "Запись игры", "棋谱");
        public static readonly LocalizationKey AddGameRecord = new LocalizationKey("Add Game Record", "Добавить запись", "添加棋谱");
        public static readonly LocalizationKey UrlOrUpload = new LocalizationKey("Enter URL or upload file", "Введите URL или загрузите файл", "输入链接或上传文件");
        public static readonly LocalizationKey Close = new LocalizationKey("Close", "Закрыть", "关闭");
        public static readonly LocalizationKey DeletePlayer = new LocalizationKey("Delete Player", "Удалить игрока", "删除选手");
        public static readonly LocalizationKey DeletePlayerConfirm = new LocalizationKey("Are you sure you want to delete this player? This will permanently delete all their matches and tournament records.", "Вы уверены, что хотите удалить этого игрока? Это безвозвратно удалит все его матчи и турнирные записи.", "确定要删除此选手吗？这将永久删除其所有对局和赛事记录。");
        public static readonly LocalizationKey PlayerDeleted = new LocalizationKey("Player and all related data deleted successfully.", "Игрок и все связанные данные успешно удалены.", "选手及所有相关数据已成功删除。");
        public static readonly LocalizationKey MatchesToDelete = new LocalizationKey("Matches to be deleted", "Матчи для удаления", "将被删除的对局");
        public static readonly LocalizationKey TournamentsAffected = new LocalizationKey("Tournaments affected", "Затронутые турниры", "受影响的赛事");
        public static readonly LocalizationKey Title = new LocalizationKey("Title", "Титул", "头衔");
        public static readonly LocalizationKey FormerTitle = new LocalizationKey("Former Title", "Бывший титул", "曾获头衔");

        private const string En = "en";
        private const string Cn = "cn";
        private const string Ru = "ru";

        private readonly Dictionary<string, string> _values;

        private LocalizationKey(string en, string ru, string cn = null)
        {
            _values = new Dictionary<string, string>
            {
                {En, en},
                {Cn, cn ?? en},
                {Ru, ru}
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