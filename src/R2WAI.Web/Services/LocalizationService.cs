using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace R2WAI.Web.Services;

public class LocalizationService
{
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations = new();
    private string _currentCulture = "en";

    public string CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                OnCultureChanged?.Invoke();
            }
        }
    }

    public event Action? OnCultureChanged;

    public string[] SupportedCultures { get; } = ["en", "es", "fr", "de", "pt", "ja", "zh", "ar"];

    public string this[string key] => Get(key);

    public string Get(string key, params object[] args)
    {
        if (_translations.TryGetValue(_currentCulture, out var dict) && dict.TryGetValue(key, out var value))
            return args.Length > 0 ? string.Format(value, args) : value;

        if (_translations.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out var fallbackValue))
            return args.Length > 0 ? string.Format(fallbackValue, args) : fallbackValue;

        return key;
    }

    public void LoadTranslations(string culture, Dictionary<string, string> translations)
    {
        _translations[culture] = translations;
    }

    public LocalizationService()
    {
        LoadTranslations("en", new Dictionary<string, string>
        {
            ["app.name"] = "R2WAI",
            ["app.tagline"] = "Enterprise AI Work Execution Platform",
            ["nav.home"] = "Home",
            ["nav.assistants"] = "Assistants",
            ["nav.knowledge"] = "Knowledge",
            ["nav.workflows"] = "Workflows",
            ["nav.chatbots"] = "Chatbots",
            ["nav.documents"] = "Documents",
            ["nav.proposals"] = "Proposals",
            ["nav.conversations"] = "Conversations",
            ["nav.inbox"] = "Inbox",
            ["nav.approvals"] = "Approvals",
            ["nav.settings"] = "Settings",
            ["nav.admin"] = "Admin",
            ["auth.login"] = "Sign In",
            ["auth.logout"] = "Sign Out",
            ["auth.email"] = "Email address",
            ["auth.password"] = "Password",
            ["auth.mfa.code"] = "Authenticator Code",
            ["auth.mfa.required"] = "Enter your authenticator code to continue.",
            ["auth.forgot_password"] = "Forgot password?",
            ["auth.remember_me"] = "Remember me",
            ["common.save"] = "Save",
            ["common.cancel"] = "Cancel",
            ["common.delete"] = "Delete",
            ["common.edit"] = "Edit",
            ["common.create"] = "Create",
            ["common.search"] = "Search",
            ["common.loading"] = "Loading...",
            ["common.no_results"] = "No results found",
            ["common.confirm_delete"] = "Are you sure you want to delete this?",
            ["error.generic"] = "An unexpected error occurred.",
            ["error.network"] = "Unable to connect to server.",
            ["error.unauthorized"] = "You are not authorized to perform this action.",
            ["success.saved"] = "Changes saved successfully.",
            ["success.deleted"] = "Item deleted successfully.",
            ["success.created"] = "Item created successfully.",
        });

        LoadTranslations("es", new Dictionary<string, string>
        {
            ["app.name"] = "R2WAI",
            ["app.tagline"] = "Plataforma Empresarial de Ejecucion de Trabajo con IA",
            ["nav.home"] = "Inicio",
            ["nav.assistants"] = "Asistentes",
            ["nav.knowledge"] = "Conocimiento",
            ["nav.workflows"] = "Flujos de Trabajo",
            ["nav.chatbots"] = "Chatbots",
            ["nav.documents"] = "Documentos",
            ["nav.proposals"] = "Propuestas",
            ["nav.conversations"] = "Conversaciones",
            ["nav.inbox"] = "Bandeja de Entrada",
            ["nav.approvals"] = "Aprobaciones",
            ["nav.settings"] = "Configuracion",
            ["nav.admin"] = "Administracion",
            ["auth.login"] = "Iniciar Sesion",
            ["auth.logout"] = "Cerrar Sesion",
            ["auth.email"] = "Correo electronico",
            ["auth.password"] = "Contrasena",
            ["auth.mfa.code"] = "Codigo de Autenticacion",
            ["auth.mfa.required"] = "Ingrese su codigo de autenticador para continuar.",
            ["auth.forgot_password"] = "Olvido su contrasena?",
            ["auth.remember_me"] = "Recordarme",
            ["common.save"] = "Guardar",
            ["common.cancel"] = "Cancelar",
            ["common.delete"] = "Eliminar",
            ["common.edit"] = "Editar",
            ["common.create"] = "Crear",
            ["common.search"] = "Buscar",
            ["common.loading"] = "Cargando...",
            ["common.no_results"] = "No se encontraron resultados",
            ["common.confirm_delete"] = "Esta seguro de que desea eliminar esto?",
            ["error.generic"] = "Ocurrio un error inesperado.",
            ["error.network"] = "No se puede conectar al servidor.",
            ["error.unauthorized"] = "No esta autorizado para realizar esta accion.",
            ["success.saved"] = "Cambios guardados exitosamente.",
            ["success.deleted"] = "Elemento eliminado exitosamente.",
            ["success.created"] = "Elemento creado exitosamente.",
        });
    }

    public string FormatDate(DateTime date) => date.ToString("g", CultureInfo.GetCultureInfo(_currentCulture));
    public string FormatNumber(decimal number) => number.ToString("N", CultureInfo.GetCultureInfo(_currentCulture));
}
