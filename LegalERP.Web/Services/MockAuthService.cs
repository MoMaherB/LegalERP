namespace LegalERP.Web.Services;

public class MockAuthService
{
    public bool IsAdmin { get; private set; } = true;
    public event Action? OnRoleChanged;

    public void ToggleRole()
    {
        IsAdmin = !IsAdmin;
        OnRoleChanged?.Invoke();
    }
}
