﻿// XAML Map Control - http://xamlmapcontrol.codeplex.com/
// Copyright © 2014 Clemens Fischer
// Licensed under the Microsoft Public License (Ms-PL)

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using FileDbNs;

namespace MapControl.Caching
{
    /// <summary>
    /// IImageCache implementation based on FileDb, a free and simple No-SQL database by EzTools Software.
    /// See http://www.eztools-software.com/tools/filedb/.
    /// </summary>
    public class FileDbCache : IImageCache, IDisposable
    {
        private const string keyField = "Key";
        private const string valueField = "Value";
        private const string expiresField = "Expires";

        private readonly FileDb fileDb = new FileDb { AutoFlush = true, AutoCleanThreshold = -1 };
        private readonly StorageFolder folder;
        private readonly string name;

        public FileDbCache(string name = null, StorageFolder folder = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = TileImageLoader.DefaultCacheName;
            }

            if (string.IsNullOrEmpty(Path.GetExtension(name)))
            {
                name += ".fdb";
            }

            if (folder == null)
            {
                folder = TileImageLoader.DefaultCacheFolder;
            }

            this.folder = folder;
            this.name = name;

            Application.Current.Resuming += async (s, e) => await Open();
            Application.Current.Suspending += (s, e) => Close();

            var task = Open();
        }

        public void Dispose()
        {
            Close();
        }

        public void Clean()
        {
            if (fileDb.IsOpen)
            {
                fileDb.DeleteRecords(new FilterExpression(expiresField, DateTime.UtcNow, ComparisonOperatorEnum.LessThan));

                if (fileDb.NumDeleted > 0)
                {
                    Debug.WriteLine("FileDbCache: Deleted {0} expired items.", fileDb.NumDeleted);
                    fileDb.Clean();
                }
            }
        }

        public async Task<ImageCacheItem> GetAsync(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("The parameter key must not be null.");
            }

            if (!fileDb.IsOpen)
            {
                return null;
            }

            return await Task.Run(() => Get(key));
        }

        public async Task SetAsync(string key, IBuffer buffer, DateTime expires)
        {
            if (key == null)
            {
                throw new ArgumentNullException("The parameter key must not be null.");
            }

            if (buffer == null)
            {
                throw new ArgumentNullException("The parameter buffer must not be null.");
            }

            if (fileDb.IsOpen)
            {
                var bytes = buffer.ToArray();
                var ok = await Task.Run(() => AddOrUpdateRecord(key, bytes, expires));

                if (!ok && (await RepairDatabase()))
                {
                    await Task.Run(() => AddOrUpdateRecord(key, bytes, expires));
                }
            }
        }

        private async Task Open()
        {
            if (!fileDb.IsOpen)
            {
                try
                {
                    var file = await folder.GetFileAsync(name);
                    var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

                    fileDb.Open(stream.AsStream());
                    Debug.WriteLine("FileDbCache: Opened database with {0} cached items in {1}.", fileDb.NumRecords, file.Path);

                    Clean();
                    return;
                }
                catch
                {
                }

                await CreateDatabase();
            }
        }

        private void Close()
        {
            if (fileDb.IsOpen)
            {
                fileDb.Close();
            }
        }

        private async Task CreateDatabase()
        {
            Close();

            var file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
            var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

            fileDb.Create(stream.AsStream(), new Field[]
            {
                new Field(keyField, DataTypeEnum.String) { IsPrimaryKey = true },
                new Field(valueField, DataTypeEnum.Byte) { IsArray = true },
                new Field(expiresField, DataTypeEnum.DateTime)
            });

            Debug.WriteLine("FileDbCache: Created database {0}.", file.Path);
        }

        private async Task<bool> RepairDatabase()
        {
            try
            {
                fileDb.Reindex();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FileDbCache: FileDb.Reindex() failed: {0}", ex.Message);
            }

            try
            {
                await CreateDatabase();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FileDbCache: Creating database {0} failed: {1}", Path.Combine(folder.Path, name), ex.Message);
            }

            return false;
        }

        private ImageCacheItem Get(string key)
        {
            var fields = new string[] { valueField, expiresField };
            Record record = null;

            try
            {
                record = fileDb.GetRecordByKey(key, fields, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FileDbCache: FileDb.GetRecordByKey(\"{0}\") failed: {1}", key, ex.Message);
            }

            if (record != null)
            {
                try
                {
                    return new ImageCacheItem
                    {
                        Buffer = ((byte[])record[0]).AsBuffer(),
                        Expires = (DateTime)record[1]
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("FileDbCache: Decoding \"{0}\" failed: {1}", key, ex.Message);
                }

                try
                {
                    fileDb.DeleteRecordByKey(key);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("FileDbCache: FileDb.DeleteRecordByKey(\"{0}\") failed: {1}", key, ex.Message);
                }
            }

            return null;
        }

        private bool AddOrUpdateRecord(string key, byte[] value, DateTime expires)
        {
            var fieldValues = new FieldValues(3);
            fieldValues.Add(valueField, value);
            fieldValues.Add(expiresField, expires);

            bool recordExists;

            try
            {
                recordExists = fileDb.GetRecordByKey(key, new string[0], false) != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FileDbCache: FileDb.GetRecordByKey(\"{0}\") failed: {1}", key, ex.Message);
                return false;
            }

            if (recordExists)
            {
                try
                {
                    fileDb.UpdateRecordByKey(key, fieldValues);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("FileDbCache: FileDb.UpdateRecordByKey(\"{0}\") failed: {1}", key, ex.Message);
                    return false;
                }
            }
            else
            {
                try
                {
                    fieldValues.Add(keyField, key);
                    fileDb.AddRecord(fieldValues);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("FileDbCache: FileDb.AddRecord(\"{0}\") failed: {1}", key, ex.Message);
                    return false;
                }
            }

            //Debug.WriteLine("Cached item {0}, expires {1}", key, expires);
            return true;
        }
    }
}