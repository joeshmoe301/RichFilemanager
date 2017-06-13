using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Web.Script.Serialization;
using System.Web.Mvc;
using System.Web.Hosting;

namespace LaurasLeanBeef_MVC.Areas.FileManager.Controllers
{
    public class FileManagerController : Controller
    {
        private readonly List<string> _allowedExtensions;
        private readonly string[] _imgExtensions = new string[] { ".jpg", ".png", ".jpeg", ".gif", ".bmp" };
        private readonly string _webPath;
        private readonly string _webRootPath;

        public FileManagerController()
        {
            // FileManager Content Folder Path
            _webPath = "/areas/richfilemanager/content";
            _webRootPath = System.Web.HttpContext.Current.Server.MapPath(_webPath);
            _allowedExtensions = new List<string> { "jpg", "jpe", "jpeg", "gif", "png", "svg", "txt", "pdf", "odp", "ods", "odt", "rtf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "csv", "ogv", "avi", "mkv", "mp4", "webm", "m4v", "ogg", "mp3", "wav", "zip", "rar", "md" };
        }

        public ActionResult Index()
        {
            //MODE
            var mode = (Request.QueryString["mode"] != null ? Request.QueryString["mode"] : Request.Form["mode"]);

            //NEW PATH
            var newPath = "";
            if (string.IsNullOrWhiteSpace(Request["new"]) == false || string.IsNullOrWhiteSpace(Request.Form["new"]) == false)
            {
                newPath = (string.IsNullOrWhiteSpace(Request["new"]) == false ? Request["new"] : Request.Form["new"]);
                if (newPath.StartsWith("/"))
                    newPath = (newPath == "/" ? string.Empty : newPath.Substring(1));
            }

            //PATH
            var path = "";
            if (string.IsNullOrWhiteSpace(Request["path"]) == false || string.IsNullOrWhiteSpace(Request.Form["path"]) == false)
            {
                path = (string.IsNullOrWhiteSpace(Request["path"]) == false ? Request["path"] : Request.Form["path"]);
                if (path.StartsWith("/"))
                    path = path.Substring(1);
            }

            //SOURCE
            var source = "";
            if (string.IsNullOrWhiteSpace(Request["source"]) == false)
            {
                if (Request["source"].StartsWith("/"))
                    source = Request["source"].Substring(1);
                else
                    source = Request["source"];
            }

            //TARGET
            var target = "";
            if (string.IsNullOrWhiteSpace(Request["target"]) == false)
            {
                if (Request["target"].StartsWith("/"))
                    target = Request["target"].Substring(1);
                else
                    target = Request["target"];
            }

            switch (mode)
            {
                case "addfolder":
                    return Json(AddFolder(path, Request["name"]), JsonRequestBehavior.AllowGet);

                case "copy":
                    return Json(Copy(source, target), JsonRequestBehavior.AllowGet);

                case "delete":
                    return Json(Delete(path), JsonRequestBehavior.AllowGet);

                case "download":
                    if (Request.Headers["accept"].ToString().Contains("json"))
                    {
                        return Json(Download(path), JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        var file = DownloadFile(path);
                        return File(file.FileBytes, "application/x-msdownload", file.FileName);
                    }

                case "editfile":
                    return Json(EditFile(path), JsonRequestBehavior.AllowGet);

                case "getfolder":
                    return Json(GetFolder(path), JsonRequestBehavior.AllowGet);

                case "getimage":
                    return GetImage(path, Convert.ToBoolean(Request["thumbnail"]));

                case "initiate":
                    return Json(Initiate(), JsonRequestBehavior.AllowGet);

                case "move":
                    return Json(Move(Request["old"], newPath), JsonRequestBehavior.AllowGet);

                case "readfile":
                    break;

                case "rename":
                    return Json(Rename(Request["old"], newPath), JsonRequestBehavior.AllowGet);

                case "savefile":
                    return Json(SaveFile(path, Request["content"]), JsonRequestBehavior.AllowGet);

                case "summarize":
                    return Json(Summarize(), JsonRequestBehavior.AllowGet);

                case "upload":
                    if (Request.Files != null)
                    {
                        var files = new List<HttpPostedFileBase>();
                        for (int i = 0; i < Request.Files.Count; i++)
                        {
                            files.Add(Request.Files[i]);
                        }
                        return Json(Upload(path, files), JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        return Json(Upload(path, new List<HttpPostedFileBase>()), JsonRequestBehavior.AllowGet);
                    }
            }

            throw new Exception("Unknown Request!");
        }

        private dynamic AddFolder(string path, string name)
        {
            var newDirectoryPath = Path.Combine(_webRootPath, path, name);

            var directoryExist = Directory.Exists(newDirectoryPath);

            if (directoryExist)
            {
                var errorResult = new { errors = new List<dynamic>() };

                errorResult.errors.Add(new
                {
                    code = "500",
                    message = "DIRECTORY_ALREADY_EXISTS",
                    arguments = new List<string>
                    {
                        name
                    }
                });

                return errorResult;
            }

            Directory.CreateDirectory(newDirectoryPath);
            var directory = new DirectoryInfo(newDirectoryPath);

            var result = new
            {
                data =
                    new
                    {
                        id = MakeWebPath(Path.Combine(path, directory.Name), false, true),
                        type = "folder",
                        attributes = new
                        {
                            name = directory.Name,
                            path = MakeWebPath(Path.Combine(_webPath, path, directory.Name), true, true),
                            readable = 1,
                            writable = 1,
                            created = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture)
                        }
                    }
            };

            return result;
        }

        private dynamic Copy(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                var errorResult = new { errors = new List<dynamic>() };

                errorResult.errors.Add(new
                {
                    code = "500",
                    id = "server",
                    message = "The previous location does not exist.",
                    title = "Server error."
                });

                return errorResult;
            }

            var fileAttributes = System.IO.File.GetAttributes(Path.Combine(_webRootPath, source));

            if (fileAttributes == FileAttributes.Directory)
            {
                var directoryName = Path.GetDirectoryName(source).Split('\\').Last();
                var newDirectoryPath = Path.Combine(target, directoryName);
                var oldPath = Path.Combine(_webRootPath, source);
                var newPath = Path.Combine(_webRootPath, target, directoryName);


                var directoryExist = Directory.Exists(newPath);

                if (directoryExist)
                {
                    var errorResult = new { errors = new List<dynamic>() };

                    errorResult.errors.Add(new
                    {
                        code = "500",
                        message = "DIRECTORY_ALREADY_EXISTS",
                        arguments = new List<string>
                        {
                            directoryName
                        }
                    });

                    return errorResult;
                }

                DirectoryCopy(oldPath, newPath);

                var result = new
                {
                    data = new
                    {
                        id = newDirectoryPath,
                        type = "folder",
                        attributes = new
                        {
                            name = directoryName,
                            readable = 1,
                            writable = 1,
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        }
                    }
                };

                return result;
            }
            else
            {
                var fileName = Path.GetFileName(source);
                var newFilePath = Path.Combine(@target, fileName);
                var oldPath = Path.Combine(_webRootPath, source);
                var newPath = Path.Combine(_webRootPath, target, fileName);

                var fileExist = System.IO.File.Exists(newPath);

                if (fileExist)
                {
                    var errorResult = new { errors = new List<dynamic>() };

                    errorResult.errors.Add(new
                    {
                        code = "500",
                        message = "FILE_ALREADY_EXISTS",
                        arguments = new List<string>
                        {
                            fileName
                        }
                    });

                    return errorResult;
                }

                System.IO.File.Copy(oldPath, newPath);

                var result = new
                {
                    data = new
                    {
                        id = newFilePath,
                        type = "file",
                        attributes = new
                        {
                            name = fileName,
                            extension = Path.GetExtension(fileName).Replace(".", ""),
                            readable = 1,
                            writable = 1,
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        }
                    }
                };

                return result;
            }
        }

        private dynamic Delete(string path)
        {
            var fileAttributes = System.IO.File.GetAttributes(Path.Combine(_webRootPath, path));

            if (fileAttributes == FileAttributes.Directory)
            {
                var directoryName = Path.GetDirectoryName(path).Split('\\').Last();

                Directory.Delete(Path.Combine(_webRootPath, path), true);

                var result = new
                {
                    data = new
                    {
                        id = path,
                        type = "folder",
                        attributes = new
                        {
                            name = directoryName,
                            readable = 1,
                            writable = 1,
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                            path = path
                        }
                    }
                };

                return result;
            }
            else
            {
                var fileName = Path.GetFileName(Path.Combine(_webRootPath, path));
                var fileExtension = Path.GetExtension(fileName).Replace(".", "");

                System.IO.File.Delete(Path.Combine(_webRootPath, path));

                var result = new
                {
                    data = new
                    {
                        id = path,
                        type = "file",
                        attributes = new
                        {
                            name = fileName,
                            extension = fileExtension,
                            readable = 1,
                            writable = 1,
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture)
                            // Path = $"/{fileName}"
                        }
                    }
                };

                return result;
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            var dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            var dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            var files = dir.GetFiles();
            foreach (var file in files)
            {
                var temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach (var subdir in dirs)
            {
                var temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        private dynamic Download(string path)
        {
            var fileName = Path.GetFileName(Path.Combine(_webRootPath, path));
            var fileExtension = Path.GetExtension(fileName).Replace(".", "");
            var result = new
            {
                data = new
                {
                    id = path,
                    type = "file",
                    attributes = new
                    {
                        name = fileName,
                        extension = fileExtension,
                        readable = 1,
                        writable = 1,
                        modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        path = $"{path}"
                    }
                }
            };

            return result;
        }

        private dynamic DownloadFile(string path)
        {
            var filepath = Path.Combine(_webRootPath, path);
            var fileName = Path.GetFileName(filepath);
            byte[] fileBytes = System.IO.File.ReadAllBytes(filepath);

            var file = new
            {
                FileName = fileName,
                FileBytes = fileBytes
            };

            return file;
        }

        private dynamic EditFile(string path)
        {
            var fileName = Path.GetFileName(path);
            var fileExtension = Path.GetExtension(path).Replace(".", "");
            var filePath = Path.Combine(_webRootPath, path);

            var content = System.IO.File.ReadAllText(filePath, Encoding.UTF8);

            var result = new
            {
                data = new
                {
                    id = path,
                    type = "file",
                    attributes = new
                    {
                        name = fileName,
                        extension = fileExtension,
                        writable = 1,
                        readable = 1,
                        content = content,
                        path = $"/{Path.Combine(path)}"
                    }
                }
            };

            return result;
        }

        private dynamic GetFolder(string path)
        {
            if (path == null)
                path = string.Empty;

            var rootpath = Path.Combine(_webRootPath, path);
            var rootDirectory = new DirectoryInfo(rootpath);
            var folderList = new List<dynamic>();

            foreach (var directory in rootDirectory.GetDirectories())
            {
                var item = new
                {
                    id = MakeWebPath(Path.Combine(path, directory.Name), false, true),
                    type = "folder",
                    attributes = new
                    {
                        name = directory.Name,
                        path = MakeWebPath(Path.Combine(_webPath, path, directory.Name), false, true),
                        readable = 1,
                        writable = 1,
                        created = directory.CreationTime.ToString(CultureInfo.InvariantCulture),
                        modified = directory.LastWriteTime.ToString(CultureInfo.InvariantCulture),
                        timestamp = (int)(DateTime.Now - directory.LastWriteTime).Ticks
                    }
                };

                folderList.Add(item);
            }

            foreach (var file in rootDirectory.GetFiles())
            {
                var item = new
                {
                    id = MakeWebPath(Path.Combine(path, file.Name)),
                    type = "file",
                    attributes = new
                    {
                        name = file.Name,
                        path = MakeWebPath(Path.Combine(_webPath, path, file.Name), false, false),
                        readable = 1,
                        writable = 1,
                        created = file.CreationTime.ToString(CultureInfo.InvariantCulture),
                        modified = file.LastWriteTime.ToString(CultureInfo.InvariantCulture),
                        extension = file.Extension.Replace(".", ""),
                        size = file.Length,
                        timestamp = (int)(DateTime.Now - file.LastWriteTime).Ticks
                    }
                };

                folderList.Add(item);
            }

            var result = new
            {
                data = folderList
            };

            return result;
        }

        private ActionResult GetImage(string path, bool thumbnail)
        {
            var filepath = Path.Combine(_webRootPath, path);
            var fileName = Path.GetFileName(filepath);
            byte[] fileBytes = System.IO.File.ReadAllBytes(filepath);

            return File(fileBytes, "application/x-msdownload", fileName);
        }

        private dynamic Initiate()
        {
            var result = new
            {
                data = new
                {
                    type = "initiate",
                    attributes = new
                    {
                        config = new
                        {
                            security = new
                            {
                                readOnly = false,
                                extensions = new
                                {
                                    ignoreCase = true,
                                    policy = "ALLOW_LIST",
                                    restrictions = _allowedExtensions
                                }
                            }
                        }
                    }
                }
            };

            return result;
        }

        private bool IsImage(string fileName)
        {
            foreach (string ext in _imgExtensions)
            {
                if (Path.GetExtension(fileName).ToLower() == ext.ToLower())
                {
                    return true;
                }
            }
            return false;
        }

        private static string MakeWebPath(string path, bool addSeperatorToBegin = false, bool addSeperatorToLast = false)
        {
            path = path.Replace("\\", "/");

            if (addSeperatorToBegin)
                path = "/" + path;

            if (addSeperatorToLast)
                path = path + "/";

            return path;
        }

        private dynamic Move(string old, string @new)
        {
            if (string.IsNullOrWhiteSpace(old))
            {
                var errorResult = new { errors = new List<dynamic>() };

                errorResult.errors.Add(new
                {
                    code = "500",
                    id = "server",
                    message = "The previous location does not exist.",
                    title = "Server error."
                });

                return errorResult;
            }

            var fileAttributes = System.IO.File.GetAttributes(Path.Combine(_webRootPath, old));

            if (fileAttributes == FileAttributes.Directory)
            {
                var directoryName = Path.GetDirectoryName(old).Split('\\').Last();
                var newDirectoryPath = Path.Combine(@new, directoryName);
                var oldPath = Path.Combine(_webRootPath, old);
                var newPath = Path.Combine(_webRootPath, @new, directoryName);
                
                if (Directory.Exists(newPath))
                {
                    var errorResult = new { errors = new List<dynamic>() };

                    errorResult.errors.Add(new
                    {
                        code = "500",
                        message = "DIRECTORY_ALREADY_EXISTS",
                        arguments = new List<string>
                        {
                            directoryName
                        }
                    });

                    return errorResult;
                }

                if (Directory.Exists(Path.Combine(_webRootPath, @new)) == false)
                {
                    var errorResult = new { errors = new List<dynamic>() };

                    errorResult.errors.Add(new
                    {
                        code = "500",
                        message = "DIRECTORY_DOES_NOT_EXIST",
                        arguments = new List<string>
                        {
                            @new
                        }
                    });

                    return errorResult;
                }

                Directory.Move(oldPath, newPath);

                var result = new
                {
                    data = new
                    {
                        id = newDirectoryPath,
                        type = "folder",
                        attributes = new
                        {
                            name = directoryName,
                            readable = 1,
                            writable = 1,
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        }
                    }
                };

                return result;
            }
            else
            {
                var fileName = Path.GetFileName(old);
                var newFilePath = Path.Combine(@new, fileName);
                var oldPath = Path.Combine(_webRootPath, old);

                var newPath = @new == "/"
                    ? Path.Combine(_webRootPath, fileName.Replace("/", ""))
                    : Path.Combine(_webRootPath, @new, fileName);


                var fileExist = System.IO.File.Exists(newPath);

                if (fileExist)
                {
                    var errorResult = new { errors = new List<dynamic>() };

                    errorResult.errors.Add(new
                    {
                        code = "500",
                        message = "FILE_ALREADY_EXISTS",
                        arguments = new List<string>
                        {
                            fileName
                        }
                    });

                    return errorResult;
                }

                System.IO.File.Move(oldPath, newPath);

                var result = new
                {
                    data = new
                    {
                        id = newFilePath,
                        type = "file",
                        attributes = new
                        {
                            name = fileName,
                            extension = Path.GetExtension(@new).Replace(".", ""),
                            readable = 1,
                            writable = 1,
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        }
                    }
                };

                return result;
            }
        }

        private dynamic Rename(string old, string @new)
        {
            var oldPath = Path.Combine(_webRootPath, old);
            var fileAttributes = System.IO.File.GetAttributes(oldPath);

            if (fileAttributes == FileAttributes.Directory)
            {
                var oldDirectoryName = Path.GetDirectoryName(old).Split('\\').Last();
                var newDirectoryPath = old.Replace(oldDirectoryName, @new);
                var newPath = Path.Combine(_webRootPath, newDirectoryPath);

                var directoryExist = Directory.Exists(newPath);

                if (directoryExist)
                {
                    var errorResult = new { errors = new List<dynamic>() };

                    errorResult.errors.Add(new
                    {
                        code = "500",
                        message = "DIRECTORY_ALREADY_EXISTS",
                        arguments = new List<string>
                        {
                            @new
                        }
                    });

                    return errorResult;
                }

                Directory.Move(oldPath, newPath);

                var result = new
                {
                    data = new
                    {
                        id = newDirectoryPath,
                        type = "folder",
                        attributes = new
                        {
                            name = @new,
                            readable = 1,
                            writable = 1,
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        }
                    }
                };

                return result;
            }
            else
            {

                var oldFileName = Path.GetFileName(old);
                var newFilePath = old.Replace(oldFileName, @new);
                var newPath = Path.Combine(_webRootPath, newFilePath);

                var fileExist = System.IO.File.Exists(newPath);

                if (fileExist)
                {
                    var errorResult = new { errors = new List<dynamic>() };

                    errorResult.errors.Add(new
                    {
                        code = "500",
                        message = "FILE_ALREADY_EXISTS",
                        arguments = new List<string>
                        {
                            @new
                        }
                    });

                    return errorResult;
                }

                System.IO.File.Move(oldPath, newPath);

                var result = new
                {
                    data = new
                    {
                        id = newFilePath,
                        type = "file",
                        attributes = new
                        {
                            name = @new,
                            extension = Path.GetExtension(newPath).Replace(".", ""),
                            readable = 1,
                            writable = 1,
                            modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        }
                    }
                };

                return result;
            }
        }

        private dynamic SaveFile(string path, string content)
        {
            var filePath = Path.Combine(_webRootPath, path);

            System.IO.File.WriteAllText(filePath, content);

            var fileName = Path.GetFileName(path);
            var fileExtension = Path.GetExtension(fileName);

            var result = new
            {
                data = new
                {
                    id = path,
                    type = "file",
                    attributes = new
                    {
                        name = fileName,
                        extension = fileExtension,
                        readable = 1,
                        writable = 1
                    }
                }
            };

            return result;
        }

        private dynamic Summarize()
        {
            var directories = Directory.GetDirectories(_webRootPath, "*", SearchOption.AllDirectories).Length;

            var directoryInfo = new DirectoryInfo(_webRootPath);
            var files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
            var allSize = files.Select(f => f.Length).Sum();

            var result = new
            {
                data = new
                {
                    id = "/",
                    type = "summary",
                    attributes = new
                    {
                        size = allSize,
                        files = files.Length,
                        folders = directories,
                        sizeLimit = 0
                    }
                }
            };

            return result;
        }

        private dynamic Upload(string path, IEnumerable<HttpPostedFileBase> files)
        {
            var fileList = new List<dynamic>();

            foreach (var file in files)
            {
                if (file.ContentLength <= 0) continue;

                var fileExist = System.IO.File.Exists(Path.Combine(_webRootPath, path, file.FileName));

                if (fileExist)
                {
                    var errorResult = new List<dynamic>();

                    errorResult.Add(new
                    {
                        code = "500",
                        message = "FILE_ALREADY_EXISTS",
                        arguments = new List<string>
                        {
                            file.FileName
                        }
                    });

                    return errorResult;
                }

                file.SaveAs(Path.Combine(_webRootPath, path, file.FileName));

                //GET HEIGHT / WIDTH
                var height = 0;
                var width = 0;

                if (IsImage(file.FileName))
                {
                    using (System.Drawing.Image img = System.Drawing.Image.FromFile(Path.Combine(_webRootPath, path, file.FileName)))
                    {
                        height = img.Height;
                        width = img.Width;
                    }
                }

                var item = new
                {
                    id = MakeWebPath(Path.Combine(path, file.FileName)),
                    type = "file",
                    attributes = new
                    {
                        name = file.FileName,
                        extension = Path.GetExtension(file.FileName).Replace(".", ""),
                        path = MakeWebPath(Path.Combine(_webPath, path, file.FileName), true),
                        readable = 1,
                        writable = 1,
                        created = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        modified = DateTime.Now.ToString(CultureInfo.InvariantCulture),
                        height = height,
                        width = width,
                        size = file.ContentLength
                    }
                };

                fileList.Add(item);
            }

            var result = new
            {
                data = fileList
            };

            return result;
        }
    }
}