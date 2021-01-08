namespace ParaTroop.Web.Services {
    public enum TroopConnectionStatus {
        Success = 0,
        HostNotFound = 1,
        InvalidData = 2,
        ConnectionTimeout = 3,
        LoginFailed = -1,
        MaxLoginsReached = -2,
        NameTaken = -3,
        VersionMismatch = -4,
        ConnectionRefused = -5,
        UnknownError = -999
    }
}
