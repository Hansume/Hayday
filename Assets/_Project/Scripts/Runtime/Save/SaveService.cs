using System;
using System.IO;
using UnityEngine;

namespace FarmGame.Save
{
    public sealed class SaveService
    {
        private readonly string _directory;
        private readonly string _fileName;

        public string SavePath => Path.Combine(_directory, _fileName);
        public string BackupPath => SavePath + ".bak";

        public SaveService(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Save directory is required.", nameof(directory));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Save file name is required.", nameof(fileName));
            }

            _directory = directory;
            _fileName = fileName;
        }

        public SaveDataV1 TryLoad(out string error)
        {
            error = null;

            if (TryLoadFile(SavePath, out var data, out var primaryError))
            {
                return data;
            }

            if (!File.Exists(SavePath))
            {
                return TryLoadFile(BackupPath, out data, out _) ? data : null;
            }

            QuarantineCorruptSave();
            if (TryLoadFile(BackupPath, out data, out _))
            {
                error = $"Primary save was invalid and was quarantined. Loaded backup. {primaryError}";
                return data;
            }

            error = primaryError;
            return null;
        }

        private static bool TryLoadFile(string path, out SaveDataV1 data, out string error)
        {
            data = null;
            error = null;

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidDataException("Save file is empty.");
                }

                data = JsonUtility.FromJson<SaveDataV1>(json);
                if (data == null)
                {
                    throw new InvalidDataException("Save file could not be parsed.");
                }

                if (data.version <= 0 || data.version > SaveDataV1.CurrentVersion)
                {
                    throw new InvalidDataException($"Unsupported save version: {data.version}.");
                }

                SaveMigrations.Apply(data);
                return true;
            }
            catch (Exception exception)
            {
                data = null;
                error = exception.Message;
                return false;
            }
        }

        public bool Save(SaveDataV1 data, out string error)
        {
            error = null;

            if (data == null)
            {
                error = "Save data is null.";
                return false;
            }

            var tempPath = SavePath + ".tmp";
            var backupPath = BackupPath;

            try
            {
                Directory.CreateDirectory(_directory);
                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(tempPath, json);

                if (File.Exists(SavePath))
                {
                    try
                    {
                        File.Replace(tempPath, SavePath, backupPath);
                    }
                    catch (Exception)
                    {
                        File.Copy(SavePath, backupPath, true);
                        File.Delete(SavePath);
                        File.Move(tempPath, SavePath);
                    }
                }
                else
                {
                    File.Move(tempPath, SavePath);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                TryDelete(tempPath);
                return false;
            }
        }

        private void QuarantineCorruptSave()
        {
            if (!File.Exists(SavePath))
            {
                return;
            }

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var quarantinePath = SavePath + ".corrupt-" + stamp;
            try
            {
                File.Move(SavePath, quarantinePath);
            }
            catch (Exception)
            {
                TryDelete(SavePath);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Best effort cleanup only.
            }
        }
    }
}
