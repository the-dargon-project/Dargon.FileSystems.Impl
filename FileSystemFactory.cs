using Dargon.IO;

namespace Dargon.FileSystem {
   public class FileSystemFactory : IFileSystemFactory {
      private readonly IDargonNodeFactory dargonNodeFactory;

      public FileSystemFactory(IDargonNodeFactory dargonNodeFactory) {
         this.dargonNodeFactory = dargonNodeFactory;
      }

      public IFileSystem CreateFromDirectory(string path) {
         var node = dargonNodeFactory.CreateFromDirectory(path);
         return new DirectoryFileSystem(node);
      }
   }
}
