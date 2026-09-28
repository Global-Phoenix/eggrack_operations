CREATE TABLE IF NOT EXISTS `eggrack_identity_migration` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_eggrack_identity_migration` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

CREATE TABLE `eggrack_identity_role` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Name` varchar(191) COLLATE utf8mb4_general_ci NULL,
    `NormalizedName` varchar(191) COLLATE utf8mb4_general_ci NULL,
    `ConcurrencyStamp` longtext COLLATE utf8mb4_general_ci NULL,
    CONSTRAINT `PK_eggrack_identity_role` PRIMARY KEY (`Id`)
) COLLATE=utf8mb4_general_ci;

CREATE TABLE `eggrack_identity_user` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `StaffRef` varchar(128) COLLATE utf8mb4_general_ci NOT NULL,
    `DisplayName` varchar(128) COLLATE utf8mb4_general_ci NOT NULL,
    `MustEnableTwoFactor` tinyint(1) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UserName` varchar(191) COLLATE utf8mb4_general_ci NULL,
    `NormalizedUserName` varchar(191) COLLATE utf8mb4_general_ci NULL,
    `Email` varchar(191) COLLATE utf8mb4_general_ci NULL,
    `NormalizedEmail` varchar(191) COLLATE utf8mb4_general_ci NULL,
    `EmailConfirmed` tinyint(1) NOT NULL,
    `PasswordHash` longtext COLLATE utf8mb4_general_ci NULL,
    `SecurityStamp` longtext COLLATE utf8mb4_general_ci NULL,
    `ConcurrencyStamp` longtext COLLATE utf8mb4_general_ci NULL,
    `PhoneNumber` longtext COLLATE utf8mb4_general_ci NULL,
    `PhoneNumberConfirmed` tinyint(1) NOT NULL,
    `TwoFactorEnabled` tinyint(1) NOT NULL,
    `LockoutEnd` datetime(6) NULL,
    `LockoutEnabled` tinyint(1) NOT NULL,
    `AccessFailedCount` int NOT NULL,
    CONSTRAINT `PK_eggrack_identity_user` PRIMARY KEY (`Id`)
) COLLATE=utf8mb4_general_ci;

CREATE TABLE `eggrack_identity_role_claim` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RoleId` bigint NOT NULL,
    `ClaimType` longtext COLLATE utf8mb4_general_ci NULL,
    `ClaimValue` longtext COLLATE utf8mb4_general_ci NULL,
    CONSTRAINT `PK_eggrack_identity_role_claim` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_eggrack_identity_role_claim_eggrack_identity_role_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `eggrack_identity_role` (`Id`) ON DELETE CASCADE
) COLLATE=utf8mb4_general_ci;

CREATE TABLE `eggrack_identity_user_claim` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` bigint NOT NULL,
    `ClaimType` longtext COLLATE utf8mb4_general_ci NULL,
    `ClaimValue` longtext COLLATE utf8mb4_general_ci NULL,
    CONSTRAINT `PK_eggrack_identity_user_claim` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_eggrack_identity_user_claim_eggrack_identity_user_UserId` FOREIGN KEY (`UserId`) REFERENCES `eggrack_identity_user` (`Id`) ON DELETE CASCADE
) COLLATE=utf8mb4_general_ci;

CREATE TABLE `eggrack_identity_user_login` (
    `LoginProvider` varchar(191) COLLATE utf8mb4_general_ci NOT NULL,
    `ProviderKey` varchar(191) COLLATE utf8mb4_general_ci NOT NULL,
    `ProviderDisplayName` longtext COLLATE utf8mb4_general_ci NULL,
    `UserId` bigint NOT NULL,
    CONSTRAINT `PK_eggrack_identity_user_login` PRIMARY KEY (`LoginProvider`, `ProviderKey`),
    CONSTRAINT `FK_eggrack_identity_user_login_eggrack_identity_user_UserId` FOREIGN KEY (`UserId`) REFERENCES `eggrack_identity_user` (`Id`) ON DELETE CASCADE
) COLLATE=utf8mb4_general_ci;

CREATE TABLE `eggrack_identity_user_role` (
    `UserId` bigint NOT NULL,
    `RoleId` bigint NOT NULL,
    CONSTRAINT `PK_eggrack_identity_user_role` PRIMARY KEY (`UserId`, `RoleId`),
    CONSTRAINT `FK_eggrack_identity_user_role_eggrack_identity_role_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `eggrack_identity_role` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_eggrack_identity_user_role_eggrack_identity_user_UserId` FOREIGN KEY (`UserId`) REFERENCES `eggrack_identity_user` (`Id`) ON DELETE CASCADE
) COLLATE=utf8mb4_general_ci;

CREATE TABLE `eggrack_identity_user_token` (
    `UserId` bigint NOT NULL,
    `LoginProvider` varchar(191) COLLATE utf8mb4_general_ci NOT NULL,
    `Name` varchar(191) COLLATE utf8mb4_general_ci NOT NULL,
    `Value` longtext COLLATE utf8mb4_general_ci NULL,
    CONSTRAINT `PK_eggrack_identity_user_token` PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
    CONSTRAINT `FK_eggrack_identity_user_token_eggrack_identity_user_UserId` FOREIGN KEY (`UserId`) REFERENCES `eggrack_identity_user` (`Id`) ON DELETE CASCADE
) COLLATE=utf8mb4_general_ci;

CREATE UNIQUE INDEX `RoleNameIndex` ON `eggrack_identity_role` (`NormalizedName`);

CREATE INDEX `IX_eggrack_identity_role_claim_RoleId` ON `eggrack_identity_role_claim` (`RoleId`);

CREATE INDEX `EmailIndex` ON `eggrack_identity_user` (`NormalizedEmail`);

CREATE UNIQUE INDEX `IX_eggrack_identity_user_StaffRef` ON `eggrack_identity_user` (`StaffRef`);

CREATE UNIQUE INDEX `UserNameIndex` ON `eggrack_identity_user` (`NormalizedUserName`);

CREATE INDEX `IX_eggrack_identity_user_claim_UserId` ON `eggrack_identity_user_claim` (`UserId`);

CREATE INDEX `IX_eggrack_identity_user_login_UserId` ON `eggrack_identity_user_login` (`UserId`);

CREATE INDEX `IX_eggrack_identity_user_role_RoleId` ON `eggrack_identity_user_role` (`RoleId`);

INSERT INTO `eggrack_identity_migration` (`MigrationId`, `ProductVersion`)
VALUES ('20260928094917_InitialInternalIdentity', '8.0.13');

COMMIT;

