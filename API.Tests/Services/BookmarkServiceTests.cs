﻿using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Reader;
using API.Entities;
using API.Entities.Enums;
using API.Services;
using API.Services.Tasks;
using API.SignalR;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class BookmarkServiceTests
{
    private readonly ILogger<CleanupService> _logger = Substitute.For<ILogger<CleanupService>>();
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<MessageHub> _messageHub = Substitute.For<IHubContext<MessageHub>>();

    private readonly DbConnection _connection;
    private readonly DataContext _context;

    private const string CacheDirectory = "C:/kavita/config/cache/";
    private const string CoverImageDirectory = "C:/kavita/config/covers/";
    private const string BackupDirectory = "C:/kavita/config/backups/";
    private const string BookmarkDirectory = "C:/kavita/config/bookmarks/";


    public BookmarkServiceTests()
    {
        var contextOptions = new DbContextOptionsBuilder()
            .UseSqlite(CreateInMemoryDatabase())
            .Options;
        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        _context = new DataContext(contextOptions);
        Task.Run(SeedDb).GetAwaiter().GetResult();

        _unitOfWork = new UnitOfWork(_context, Substitute.For<IMapper>(), null);
    }

    #region Setup

    private static DbConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");

        connection.Open();

        return connection;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<bool> SeedDb()
    {
        await _context.Database.MigrateAsync();
        var filesystem = CreateFileSystem();

        await Seed.SeedSettings(_context, new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

        var setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
        setting.Value = CacheDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
        setting.Value = BackupDirectory;

        setting = await _context.ServerSetting.Where(s => s.Key == ServerSettingKey.BookmarkDirectory).SingleAsync();
        setting.Value = BookmarkDirectory;

        _context.ServerSetting.Update(setting);

        _context.Library.Add(new Library()
        {
            Name = "Manga",
            Folders = new List<FolderPath>()
            {
                new FolderPath()
                {
                    Path = "C:/data/"
                }
            }
        });
        return await _context.SaveChangesAsync() > 0;
    }

    private async Task ResetDB()
    {
        _context.Series.RemoveRange(_context.Series.ToList());
        _context.Users.RemoveRange(_context.Users.ToList());
        _context.AppUserBookmark.RemoveRange(_context.AppUserBookmark.ToList());

        await _context.SaveChangesAsync();
    }

    private static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.SetCurrentDirectory("C:/kavita/");
        fileSystem.AddDirectory("C:/kavita/config/");
        fileSystem.AddDirectory(CacheDirectory);
        fileSystem.AddDirectory(CoverImageDirectory);
        fileSystem.AddDirectory(BackupDirectory);
        fileSystem.AddDirectory(BookmarkDirectory);
        fileSystem.AddDirectory("C:/data/");

        return fileSystem;
    }

    #endregion

    #region BookmarkPage

    [Fact]
    public async Task BookmarkPage_ShouldCopyTheFileAndUpdateDB()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {

                        }
                    }
                }
            }
        });

        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe"
        });

        await _context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = new BookmarkService(Substitute.For<ILogger<BookmarkService>>(), _unitOfWork, ds);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);

        var result = await bookmarkService.BookmarkPage(user, new BookmarkDto()
        {
            ChapterId = 1,
            Page = 1,
            SeriesId = 1,
            VolumeId = 1
        }, $"{CacheDirectory}1/0001.jpg");


        Assert.True(result);
        Assert.Equal(1, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
        Assert.NotNull(await _unitOfWork.UserRepository.GetBookmarkAsync(1));
    }

    [Fact]
    public async Task BookmarkPage_ShouldDeleteFileOnUnbookmark()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/0001.jpg", new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {

                        }
                    }
                }
            }
        });


        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe",
            Bookmarks = new List<AppUserBookmark>()
            {
                new AppUserBookmark()
                {
                    Page = 1,
                    ChapterId = 1,
                    FileName = $"1/1/0001.jpg",
                    SeriesId = 1,
                    VolumeId = 1
                }
            }
        });

        await _context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = new BookmarkService(Substitute.For<ILogger<BookmarkService>>(), _unitOfWork, ds);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(1, AppUserIncludes.Bookmarks);

        var result = await bookmarkService.RemoveBookmarkPage(user, new BookmarkDto()
        {
            ChapterId = 1,
            Page = 1,
            SeriesId = 1,
            VolumeId = 1
        });


        Assert.True(result);
        Assert.Equal(0, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
        Assert.Null(await _unitOfWork.UserRepository.GetBookmarkAsync(1));
    }

    #endregion

    #region DeleteBookmarkFiles

    [Fact]
    public async Task DeleteBookmarkFiles_ShouldDeleteOnlyPassedFiles()
    {
        var filesystem = CreateFileSystem();
        filesystem.AddFile($"{CacheDirectory}1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/1/1/0001.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/2/1/0002.jpg", new MockFileData("123"));
        filesystem.AddFile($"{BookmarkDirectory}1/2/1/0001.jpg", new MockFileData("123"));

        // Delete all Series to reset state
        await ResetDB();

        _context.Series.Add(new Series()
        {
            Name = "Test",
            Library = new Library() {
                Name = "Test LIb",
                Type = LibraryType.Manga,
            },
            Volumes = new List<Volume>()
            {
                new Volume()
                {
                    Chapters = new List<Chapter>()
                    {
                        new Chapter()
                        {

                        }
                    }
                }
            }
        });


        _context.AppUser.Add(new AppUser()
        {
            UserName = "Joe",
            Bookmarks = new List<AppUserBookmark>()
            {
                new AppUserBookmark()
                {
                    Page = 1,
                    ChapterId = 1,
                    FileName = $"1/1/1/0001.jpg",
                    SeriesId = 1,
                    VolumeId = 1
                },
                new AppUserBookmark()
                {
                    Page = 2,
                    ChapterId = 1,
                    FileName = $"1/2/1/0002.jpg",
                    SeriesId = 2,
                    VolumeId = 1
                },
                new AppUserBookmark()
                {
                    Page = 1,
                    ChapterId = 2,
                    FileName = $"1/2/1/0001.jpg",
                    SeriesId = 2,
                    VolumeId = 1
                }
            }
        });

        await _context.SaveChangesAsync();


        var ds = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem);
        var bookmarkService = new BookmarkService(Substitute.For<ILogger<BookmarkService>>(), _unitOfWork, ds);

        await bookmarkService.DeleteBookmarkFiles(new [] {new AppUserBookmark()
        {
            Page = 1,
            ChapterId = 1,
            FileName = $"1/1/1/0001.jpg",
            SeriesId = 1,
            VolumeId = 1
        }});


        Assert.Equal(2, ds.GetFiles(BookmarkDirectory, searchOption:SearchOption.AllDirectories).Count());
        Assert.False(ds.FileSystem.FileInfo.FromFileName(Path.Join(BookmarkDirectory, "1/1/1/0001.jpg")).Exists);
    }
    #endregion
}