using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using Dargon.IO;
using ItzWarty;
using ItzWarty.Collections;
using System.Collections.Generic;
using System.Threading;
using ItzWarty.IO;

namespace Dargon.FileSystem {
   public class DirectoryFileSystem : IFileSystem {
      private readonly IWritableDargonNode baseNode;
      private readonly ConcurrentDictionary<IReadableDargonNode, InternalHandle> handlesByNode = new ConcurrentDictionary<IReadableDargonNode, InternalHandle>();

      public DirectoryFileSystem(IWritableDargonNode baseNode) {
         this.baseNode = baseNode;
      }

      public IFileSystemHandle AllocateRootHandle() {
         return GetNodeHandle(baseNode);
      }

      public IoResult AllocateChildrenHandles(IFileSystemHandle handle, out IFileSystemHandle[] childHandles) {
         var internalHandle = handle as InternalHandle;
         if (internalHandle == null) {
            childHandles = null;
            return IoResult.InvalidHandle;
         }

         var children = internalHandle.Node.Children;
         childHandles = Util.Generate(children.Count, i => GetNodeHandle(children[i]));
         return IoResult.Success;
      }

      public IoResult AllocateRelativeHandleFromPath(IFileSystemHandle baseNode, string relativePath, out IFileSystemHandle handle) {
         var internalHandle = baseNode as InternalHandle;
         handle = null;

         if (internalHandle == null || internalHandle.State == HandleState.Invalidated || internalHandle.State == HandleState.Disposed) {
            return IoResult.InvalidHandle;
         }

         var foundNode = internalHandle.Node.GetRelativeOrNull(relativePath);

         if (foundNode == null) {
            return IoResult.NotFound;
         }

         handle = GetNodeHandle(foundNode);
         return IoResult.Success;
      }

      public IoResult ReadAllBytes(IFileSystemHandle handle, out byte[] bytes) {
         var internalHandle = handle as InternalHandle;
         if (internalHandle == null) {
            bytes = null;
            return IoResult.InvalidHandle;
         }

         var path = internalHandle.Node.GetPath();
         var fileInfo = new FileInfo(path);
         if (!fileInfo.Exists) {
            bytes = null;
            return IoResult.NotFound;
         } else if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)) {
            bytes = null;
            return IoResult.InvalidOperation;
         }
         bytes = File.ReadAllBytes(path);
         return IoResult.Success;
      }

      public void FreeHandle(IFileSystemHandle handle) {
         var internalHandle = handle as InternalHandle;
         if (internalHandle != null) {
            var referenceCount = internalHandle.DecrementReferenceCount();
            if (referenceCount == 0) {
               lock (internalHandle) {
                  if (internalHandle.GetReferenceCount() == 0) {
                     internalHandle.Invalidate();
                  }
               }
            }
         }
      }

      public void FreeHandles(IEnumerable<IFileSystemHandle> handles) {
         handles.ForEach(FreeHandle);
      }

      public IoResult GetName(IFileSystemHandle handle, out string name) {
         var internalHandle = handle as InternalHandle;

         if (internalHandle == null || internalHandle.State == HandleState.Invalidated || internalHandle.State == HandleState.Disposed) {
            name = null;
            return IoResult.InvalidHandle;
         }

         name = internalHandle.Node.Name;
         return IoResult.Success;

      }

      public IoResult GetPath(IFileSystemHandle handle, out string path) {
         var internalHandle = handle as InternalHandle;

         if (internalHandle == null || internalHandle.State == HandleState.Invalidated || internalHandle.State == HandleState.Disposed) {
            path = null;
            return IoResult.InvalidHandle;
         }

         path = internalHandle.Node.GetPath();
         return IoResult.Success;
      }

      private InternalHandle GetNodeHandle(IReadableDargonNode node) {
         return handlesByNode.AddOrUpdate(node, n => new InternalHandle(n), (n, h) => {
            h.IncrementReferenceCount();
            return h;
         });
      }

      private class InternalHandle : IFileSystemHandle {
         private IReadableDargonNode node;
         private HandleState state;
         private int referenceCount;

         public InternalHandle(IReadableDargonNode node) {
            this.node = node;
            this.state = HandleState.Valid;
            this.referenceCount = 0;
         }

         public HandleState State { get { return state; } }
         public IReadableDargonNode Node { get { return node; } }

         public int GetReferenceCount() { return referenceCount; }
         public int IncrementReferenceCount() { return Interlocked.Increment(ref referenceCount); }
         public int DecrementReferenceCount() { return Interlocked.Decrement(ref referenceCount); }

         public override string ToString() { return "[DFS Handle to " + Node.GetPath() + " ]"; }

         public void Invalidate() {
            state = HandleState.Invalidated;
         }
      }
   }
}
