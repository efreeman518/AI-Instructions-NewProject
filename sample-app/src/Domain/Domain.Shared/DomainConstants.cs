namespace Domain.Shared;

public static class DomainConstants
{
    public const int TITLE_MAX_LENGTH = 200;
    public const int DESCRIPTION_MAX_LENGTH = 2000;
    public const int NAME_MAX_LENGTH = 100;
    public const int TAG_NAME_MAX_LENGTH = 50;
    public const int COMMENT_MAX_LENGTH = 1000;
    public const int URL_MAX_LENGTH = 2048;
    public const int COLOR_HEX_LENGTH = 7; // #RRGGBB
    public const int PRIORITY_MIN = 1;
    public const int PRIORITY_MAX = 5;
    public const int PRIORITY_DEFAULT = 3;
    public const int HIERARCHY_MAX_DEPTH = 5;
    public const int HISTORY_RETENTION_DAYS = 90;
}
