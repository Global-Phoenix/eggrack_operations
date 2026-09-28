SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

CREATE TABLE dbo.SysUsers (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SysUsers PRIMARY KEY,
    UserName nvarchar(64) NOT NULL,
    DisplayName nvarchar(100) NOT NULL,
    PasswordHash nvarchar(500) NOT NULL,
    Email nvarchar(256) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_SysUsers_IsEnabled DEFAULT(1),
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SysUsers_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
    CreatedBy bigint NULL, UpdatedAtUtc datetime2(3) NULL, UpdatedBy bigint NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_SysUsers_IsDeleted DEFAULT(0)
);
CREATE UNIQUE INDEX UX_SysUsers_UserName ON dbo.SysUsers(UserName) WHERE IsDeleted = 0;

CREATE TABLE dbo.SysRoles (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SysRoles PRIMARY KEY,
    Code varchar(80) NOT NULL, Name nvarchar(100) NOT NULL, Description nvarchar(500) NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_SysRoles_IsEnabled DEFAULT(1),
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SysRoles_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
    CreatedBy bigint NULL, UpdatedAtUtc datetime2(3) NULL, UpdatedBy bigint NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_SysRoles_IsDeleted DEFAULT(0)
);
CREATE UNIQUE INDEX UX_SysRoles_Code ON dbo.SysRoles(Code) WHERE IsDeleted = 0;

CREATE TABLE dbo.SysPermissions (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SysPermissions PRIMARY KEY,
    Code varchar(160) NOT NULL, Name nvarchar(100) NOT NULL, Module nvarchar(80) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SysPermissions_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
    CreatedBy bigint NULL, UpdatedAtUtc datetime2(3) NULL, UpdatedBy bigint NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_SysPermissions_IsDeleted DEFAULT(0)
);
CREATE UNIQUE INDEX UX_SysPermissions_Code ON dbo.SysPermissions(Code) WHERE IsDeleted = 0;

CREATE TABLE dbo.SysUserRoles (
    UserId bigint NOT NULL, RoleId bigint NOT NULL,
    CONSTRAINT PK_SysUserRoles PRIMARY KEY(UserId, RoleId),
    CONSTRAINT FK_SysUserRoles_User FOREIGN KEY(UserId) REFERENCES dbo.SysUsers(Id),
    CONSTRAINT FK_SysUserRoles_Role FOREIGN KEY(RoleId) REFERENCES dbo.SysRoles(Id)
);
CREATE TABLE dbo.SysRolePermissions (
    RoleId bigint NOT NULL, PermissionId bigint NOT NULL,
    CONSTRAINT PK_SysRolePermissions PRIMARY KEY(RoleId, PermissionId),
    CONSTRAINT FK_SysRolePermissions_Role FOREIGN KEY(RoleId) REFERENCES dbo.SysRoles(Id),
    CONSTRAINT FK_SysRolePermissions_Permission FOREIGN KEY(PermissionId) REFERENCES dbo.SysPermissions(Id)
);

CREATE TABLE dbo.SysMenus (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_SysMenus PRIMARY KEY,
    ParentId bigint NULL, Name nvarchar(100) NOT NULL, Code varchar(120) NOT NULL,
    Area varchar(80) NULL, Controller varchar(80) NULL, Action varchar(80) NULL,
    Icon varchar(80) NULL, PermissionCode varchar(160) NULL,
    SortOrder int NOT NULL CONSTRAINT DF_SysMenus_SortOrder DEFAULT(0),
    IsVisible bit NOT NULL CONSTRAINT DF_SysMenus_IsVisible DEFAULT(1),
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SysMenus_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
    CreatedBy bigint NULL, UpdatedAtUtc datetime2(3) NULL, UpdatedBy bigint NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_SysMenus_IsDeleted DEFAULT(0),
    CONSTRAINT FK_SysMenus_Parent FOREIGN KEY(ParentId) REFERENCES dbo.SysMenus(Id)
);
CREATE UNIQUE INDEX UX_SysMenus_Code ON dbo.SysMenus(Code) WHERE IsDeleted = 0;
CREATE INDEX IX_SysMenus_Parent_Sort ON dbo.SysMenus(ParentId, SortOrder);

CREATE TABLE dbo.FileAssets (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FileAssets PRIMARY KEY,
    OriginalName nvarchar(260) NOT NULL, StorageName nvarchar(260) NOT NULL,
    StorageProvider varchar(40) NOT NULL, RelativePath nvarchar(1000) NOT NULL,
    ContentType varchar(200) NULL, Length bigint NOT NULL, Sha256 char(64) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_FileAssets_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
    CreatedBy bigint NULL, UpdatedAtUtc datetime2(3) NULL, UpdatedBy bigint NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_FileAssets_IsDeleted DEFAULT(0)
);
CREATE INDEX IX_FileAssets_CreatedAtUtc ON dbo.FileAssets(CreatedAtUtc DESC);

CREATE TABLE dbo.BackgroundTasks (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_BackgroundTasks PRIMARY KEY,
    TaskType varchar(100) NOT NULL, Name nvarchar(200) NOT NULL, Status varchar(30) NOT NULL,
    Progress int NOT NULL CONSTRAINT DF_BackgroundTasks_Progress DEFAULT(0),
    PayloadJson nvarchar(max) NULL, ResultJson nvarchar(max) NULL, ErrorMessage nvarchar(max) NULL,
    StartedAtUtc datetime2(3) NULL, CompletedAtUtc datetime2(3) NULL,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_BackgroundTasks_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
    CreatedBy bigint NULL, UpdatedAtUtc datetime2(3) NULL, UpdatedBy bigint NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_BackgroundTasks_IsDeleted DEFAULT(0),
    CONSTRAINT CK_BackgroundTasks_Progress CHECK(Progress BETWEEN 0 AND 100)
);
CREATE INDEX IX_BackgroundTasks_Status_Created ON dbo.BackgroundTasks(Status, CreatedAtUtc DESC);

CREATE TABLE dbo.OperationLogs (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OperationLogs PRIMARY KEY,
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_OperationLogs_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
    UserId bigint NULL, UserName nvarchar(64) NULL, Category varchar(80) NOT NULL,
    Action varchar(120) NOT NULL, TargetType varchar(100) NULL, TargetId nvarchar(100) NULL,
    Description nvarchar(2000) NULL, IpAddress varchar(64) NULL, TraceId varchar(100) NULL,
    Succeeded bit NOT NULL
);
CREATE INDEX IX_OperationLogs_CreatedAtUtc ON dbo.OperationLogs(CreatedAtUtc DESC);
CREATE INDEX IX_OperationLogs_UserId_Created ON dbo.OperationLogs(UserId, CreatedAtUtc DESC);

CREATE TABLE dbo.EmailTemplates (
    Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmailTemplates PRIMARY KEY,
    Code varchar(120) NOT NULL, Name nvarchar(160) NOT NULL,
    SubjectTemplate nvarchar(500) NOT NULL, BodyTemplate nvarchar(max) NOT NULL,
    IsHtml bit NOT NULL CONSTRAINT DF_EmailTemplates_IsHtml DEFAULT(1),
    IsEnabled bit NOT NULL CONSTRAINT DF_EmailTemplates_IsEnabled DEFAULT(1),
    CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_EmailTemplates_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
    CreatedBy bigint NULL, UpdatedAtUtc datetime2(3) NULL, UpdatedBy bigint NULL,
    IsDeleted bit NOT NULL CONSTRAINT DF_EmailTemplates_IsDeleted DEFAULT(0)
);
CREATE UNIQUE INDEX UX_EmailTemplates_Code ON dbo.EmailTemplates(Code) WHERE IsDeleted = 0;

COMMIT TRANSACTION;
