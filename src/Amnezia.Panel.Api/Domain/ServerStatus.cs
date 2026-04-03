namespace Amnezia.Panel.Api.Domain;

public enum ServerStatus
{
    Provisioning = 0,
    Active = 1,
    Degraded = 2,
    Error = 3,
    Stopped = 4
}
