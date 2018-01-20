create user identityws@'localhost' identified by 'development';
create database identityws character set utf8mb4 collate utf8mb4_unicode_ci;
grant all on identityws.* to identityws@'localhost';
