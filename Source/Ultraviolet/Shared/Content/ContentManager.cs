﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ultraviolet.Core;
using Ultraviolet.Core.Messages;
using Ultraviolet.Core.Xml;
using Ultraviolet.Platform;

namespace Ultraviolet.Content
{
    /// <summary>
    /// Represents a collection of related content assets.
    /// </summary>
    public sealed partial class ContentManager : UltravioletResource, IMessageSubscriber<UltravioletMessageID>
    {
        /// <summary>
        /// Initializes the <see cref="ContentManager"/> type.
        /// </summary>
        static ContentManager()
        {
            ScreenDensityBuckets = ((ScreenDensityBucket[])Enum.GetValues(typeof(ScreenDensityBucket))).OrderByDescending(x => x);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentManager"/> class with the specified root directory.
        /// </summary>
        /// <param name="uv">The Ultraviolet context.</param>
        /// <param name="rootDirectory">The content manager's root directory.</param>
        private ContentManager(UltravioletContext uv, String rootDirectory)
            : base(uv)
        {
            this.RootDirectory = rootDirectory;
            this.FullRootDirectory = (rootDirectory == null) ? Directory.GetCurrentDirectory() : Path.GetFullPath(rootDirectory);
            this.fileSystemService = FileSystemService.Create();
            this.overrideDirectories = new ContentOverrideDirectoryCollection(this);

            uv.Messages.Subscribe(this, UltravioletMessages.LowMemory, UltravioletMessages.DisplayDensityChanged);
        }

        /// <summary>
        /// Creates a new <see cref="ContentManager"/> with the specified root directory.
        /// </summary>
        /// <param name="rootDirectory">The content manager's root directory.</param>
        /// <returns>The <see cref="ContentManager"/> that was created.</returns>
        public static ContentManager Create(String rootDirectory = null)
        {
            var uv = UltravioletContext.DemandCurrent();
            return new ContentManager(uv, rootDirectory);
        }

        /// <summary>
        /// Creates a new <see cref="ContentManager"/> with the specified root directory.
        /// </summary>
        /// <param name="rootDirectoryPaths">An array containing the parts of the path to the content manager's root directory.</param>
        /// <returns>The <see cref="ContentManager"/> that was created.</returns>
        public static ContentManager Create(params String[] rootDirectoryPaths)
        {
            return Create(Path.Combine(rootDirectoryPaths));
        }

        /// <summary>
        /// Converts the specified asset path to a normalized asset path.
        /// </summary>
        /// <param name="path">The asset path to normalize.</param>
        /// <returns>The normalized asset path.</returns>
        public static String NormalizeAssetPath(String path)
        {
            // Make sure the path isn't rooted.
            if (Path.IsPathRooted(path))
                throw new ArgumentException(UltravioletStrings.AssetPathMustBeRelative);

            // Remove any directory traversal operators.
            var stack = new Stack<String>();
            var parts = path.Split('/', '\\');
            foreach (var part in parts)
            {
                if (part == ".") { continue; }
                if (part == "..")
                {
                    if (stack.Count == 0)
                    {
                        throw new InvalidOperationException(UltravioletStrings.AssetPathCannotTraverseDirectories);
                    }
                    stack.Pop();
                    continue;
                }
                stack.Push(part);
            }

            // Recombine the path parts into a single string.
            var sb = new StringBuilder();
            foreach (var item in stack)
            {
                if (sb.Length > 0)
                {
                    sb.Insert(0, Path.DirectorySeparatorChar);
                }
                sb.Insert(0, item);
            }
            return sb.ToString();
        }

        /// <inheritdoc/>
        void IMessageSubscriber<UltravioletMessageID>.ReceiveMessage(UltravioletMessageID type, MessageData data)
        {
            if (type == UltravioletMessages.LowMemory)
            {
                PurgeCache(true);
                return;
            }

            if (type == UltravioletMessages.DisplayDensityChanged)
            {
                PurgeUnusedScreenDensities();
                return;
            }
        }

        /// <summary>
        /// Purges the specified asset from the content manager's internal cache.
        /// </summary>
        /// <param name="asset">The asset to purge from the cache.</param>
        /// <param name="lowMemory">A value indicating whether the cache is being purged due to the operating system
        /// being low on memory. If this value is <see langword="true"/>, then assets which have the 
        /// <see cref="AssetFlags.PreserveThroughLowMemory"/> flag will be ignored by this method. Otherwise,
        /// all of the cache's assets will be purged.</param>
        public void PurgeCache(String asset, Boolean lowMemory)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
            {
                if (!GetAssetFlags(asset, out var flags))
                    return;

                var preserve = (flags & AssetFlags.PreserveThroughLowMemory) == AssetFlags.PreserveThroughLowMemory;
                if (preserve && lowMemory)
                    return;

                assetCache.Remove(asset);
                ClearAssetDependencies(asset);
            }
        }

        /// <summary>
        /// Purges the specified asset from the content manager's internal cache.
        /// </summary>
        /// <param name="asset">The asset to purge from the cache.</param>
        /// <param name="lowMemory">A value indicating whether the cache is being purged due to the operating system
        /// being low on memory. If this value is <see langword="true"/>, then assets which have the 
        /// <see cref="AssetFlags.PreserveThroughLowMemory"/> flag will be ignored by this method. Otherwise,
        /// all of the cache's assets will be purged.</param>
        public void PurgeCache(AssetID asset, Boolean lowMemory)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));

            PurgeCache(AssetID.GetAssetPath(asset), lowMemory);
        }

        /// <summary>
        /// Purges the content manager's internal cache, removing all references to previously loaded objects
        /// so that they can be collected.
        /// </summary>
        /// <param name="lowMemory">A value indicating whether the cache is being purged due to the operating system
        /// being low on memory. If this value is <see langword="true"/>, then assets which have the 
        /// <see cref="AssetFlags.PreserveThroughLowMemory"/> flag will be ignored by this method. Otherwise,
        /// all of the cache's assets will be purged.</param>
        public void PurgeCache(Boolean lowMemory)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
            {
                foreach (var kvp in assetFlags)
                {
                    var asset = kvp.Key;
                    var flags = kvp.Value;

                    var preserve = (flags & AssetFlags.PreserveThroughLowMemory) == AssetFlags.PreserveThroughLowMemory;
                    if (preserve && lowMemory)
                        continue;

                    assetCache.Remove(asset);
                    ClearAssetDependencies(asset);
                }
            }
        }

        /// <summary>
        /// Sets the flags associated with the specified asset.
        /// </summary>
        /// <remarks>Please note that, for performance reasons, the content manager's internal cache does not normalize asset paths.
        /// This means that if you reference the same asset by two different but equivalent paths (i.e. "foo/bar" and "foo\\bar"), 
        /// each of those paths will represent a <b>separate entry in the cache</b> with <b>separate asset flags</b>.</remarks>
        /// <param name="asset">The asset path of the asset for which to set flags.</param>
        /// <param name="flags">A collection of <see cref="AssetFlags"/> values to associate with the specified asset.</param>
        public void SetAssetFlags(String asset, AssetFlags flags)
        {
            Contract.Require(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
                assetFlags[asset] = flags;
        }

        /// <summary>
        /// Sets the flags associated with the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to set flags.</param>
        /// <param name="flags">A collection of <see cref="AssetFlags"/> values to associate with the specified asset.</param>
        public void SetAssetFlags(AssetID asset, AssetFlags flags)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
                assetFlags[AssetID.GetAssetPath(asset)] = flags;
        }

        /// <summary>
        /// Sets the flags associated with the specified asset.
        /// </summary>
        /// <remarks>Please note that, for performance reasons, the content manager's internal cache does not normalize asset paths.
        /// This means that if you reference the same asset by two different but equivalent paths (i.e. "foo/bar" and "foo\\bar"), 
        /// each of those paths will represent a <b>separate entry in the cache</b> with <b>separate asset flags</b>.</remarks>
        /// <param name="asset">The asset path of the asset for which to retrieve flags.</param>
        /// <param name="flags">A collection of <see cref="AssetFlags"/> value associated with the specified asset.</param>
        /// <returns><see langword="true"/> if the specified asset has flags defined within this 
        /// content manager; otherwise, <see langword="false"/>.</returns>
        public Boolean GetAssetFlags(String asset, out AssetFlags flags)
        {
            Contract.Require(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
                return assetFlags.TryGetValue(asset, out flags);
        }

        /// <summary>
        /// Sets the flags associated with the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to retrieve flags.</param>
        /// <param name="flags">A collection of <see cref="AssetFlags"/> value associated with the specified asset.</param>
        /// <returns><see langword="true"/> if the specified asset has flags defined within this 
        /// content manager; otherwise, <see langword="false"/>.</returns>
        public Boolean GetAssetFlags(AssetID asset, out AssetFlags flags)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
                return assetFlags.TryGetValue(AssetID.GetAssetPath(asset), out flags);
        }

        /// <summary>
        /// Adds a watcher for the specified asset.
        /// </summary>
        /// <param name="asset">The asset path of the asset for which to add a watcher.</param>
        /// <param name="watcher">The watcher to add for the specified asset.</param>
        /// <returns><see langword="true"/> if the watcher was added; otherwise, <see langword="false"/>.</returns>
        public Boolean AddWatcher<TOutput>(String asset, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensityBucket = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return AddWatcherInternal(asset, primaryDisplayDensityBucket, watcher);
        }

        /// <summary>
        /// Adds a watcher for the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to add a watcher.</param>
        /// <param name="watcher">The watcher to add for the specified asset.</param>
        /// <returns><see langword="true"/> if the watcher was added; otherwise, <see langword="false"/>.</returns>
        public Boolean AddWatcher<TOutput>(AssetID asset, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensityBucket = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return AddWatcherInternal(AssetID.GetAssetPath(asset), primaryDisplayDensityBucket, watcher);
        }

        /// <summary>
        /// Adds a watcher for the specified asset.
        /// </summary>
        /// <param name="asset">The asset path of the asset for which to add a watcher.</param>
        /// <param name="density">The density bucket corresponding to the version of the asset to watch.</param>
        /// <param name="watcher">The watcher to add for the specified asset.</param>
        /// <returns><see langword="true"/> if the watcher was added; otherwise, <see langword="false"/>.</returns>
        public Boolean AddWatcher<TOutput>(String asset, ScreenDensityBucket density, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);

            return AddWatcherInternal(asset, density, watcher);
        }

        /// <summary>
        /// Adds a watcher for the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to add a watcher.</param>
        /// <param name="density">The density bucket corresponding to the version of the asset to watch.</param>
        /// <param name="watcher">The watcher to add for the specified asset.</param>
        /// <returns><see langword="true"/> if the watcher was added; otherwise, <see langword="false"/>.</returns>
        public Boolean AddWatcher<TOutput>(AssetID asset, ScreenDensityBucket density, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);

            return AddWatcherInternal(AssetID.GetAssetPath(asset), density, watcher);
        }

        /// <summary>
        /// Removes a watcher from the specified asset.
        /// </summary>
        /// <param name="asset">The asset path of the asset for which to remove a watcher.</param>
        /// <param name="watcher">The watcher to remove from the specified asset.</param>
        /// <returns><see langword="true"/> if the specified watcher was removed; otherwise, <see langword="false"/>.</returns>
        public Boolean RemoveWatcher<TOutput>(String asset, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensityBucket = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return RemoveWatcherInternal(asset, primaryDisplayDensityBucket, watcher);
        }

        /// <summary>
        /// Removes a watcher from the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to remove a watcher.</param>
        /// <param name="watcher">The watcher to remove from the specified asset.</param>
        /// <returns><see langword="true"/> if the specified watcher was removed; otherwise, <see langword="false"/>.</returns>
        public Boolean RemoveWatcher<TOutput>(AssetID asset, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensityBucket = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return RemoveWatcherInternal(AssetID.GetAssetPath(asset), primaryDisplayDensityBucket, watcher);
        }

        /// <summary>
        /// Removes a watcher from the specified asset.
        /// </summary>
        /// <param name="asset">The asset path of the asset for which to remove a watcher.</param>
        /// <param name="density">The density bucket corresponding to the version of the asset to watch.</param>
        /// <param name="watcher">The watcher to remove from the specified asset.</param>
        /// <returns><see langword="true"/> if the specified watcher was removed; otherwise, <see langword="false"/>.</returns>
        public Boolean RemoveWatcher<TOutput>(String asset, ScreenDensityBucket density, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);
            
            return RemoveWatcherInternal(asset, density, watcher);
        }

        /// <summary>
        /// Removes a watcher from the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to remove a watcher.</param>
        /// <param name="density">The density bucket corresponding to the version of the asset to watch.</param>
        /// <param name="watcher">The watcher to remove from the specified asset.</param>
        /// <returns><see langword="true"/> if the specified watcher was removed; otherwise, <see langword="false"/>.</returns>
        public Boolean RemoveWatcher<TOutput>(AssetID asset, ScreenDensityBucket density, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);
            
            return RemoveWatcherInternal(AssetID.GetAssetPath(asset), density, watcher);
        }

        /// <summary>
        /// Adds the specified dependency to an asset. If the asset is being watched for changes, then any
        /// changes to the specified dependency will also cause the main asset to be reloaded.
        /// </summary>
        /// <param name="asset">The asset path of the asset for which to add a dependency.</param>
        /// <param name="dependency">The asset path of the dependency to add to the specified asset.</param>
        public void AddAssetDependency(String asset, String dependency)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            AddAssetDependencyInternal(asset, dependency, primaryDisplayDensity);
        }

        /// <summary>
        /// Adds the specified dependency to an asset. If the asset is being watched for changes, then any
        /// changes to the specified dependency will also cause the main asset to be reloaded.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to add a dependency.</param>
        /// <param name="dependency">The asset identifier of the dependency to add to the specified asset.</param>
        public void AddAssetDependency(AssetID asset, AssetID dependency)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.Ensure<ArgumentException>(dependency.IsValid, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            AddAssetDependencyInternal(AssetID.GetAssetPath(asset), AssetID.GetAssetPath(dependency), primaryDisplayDensity);
        }

        /// <summary>
        /// Adds the specified dependency to an asset. If the asset is being watched for changes, then any
        /// changes to the specified dependency will also cause the main asset to be reloaded.
        /// </summary>
        /// <param name="asset">The asset path of the asset for which to add a dependency.</param>
        /// <param name="dependency">The asset path of the dependency to add to the specified asset.</param>
        /// <param name="density">The screen density of the assets for which to add a dependency.</param>
        public void AddAssetDependency(String asset, String dependency, ScreenDensityBucket density)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);
            
            AddAssetDependencyInternal(asset, dependency, density);
        }

        /// <summary>
        /// Adds the specified dependency to an asset. If the asset is being watched for changes, then any
        /// changes to the specified dependency will also cause the main asset to be reloaded.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to add a dependency.</param>
        /// <param name="dependency">The asset identifier of the dependency to add to the specified asset.</param>
        /// <param name="density">The screen density of the assets for which to add a dependency.</param>
        public void AddAssetDependency(AssetID asset, AssetID dependency, ScreenDensityBucket density)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.Ensure<ArgumentException>(dependency.IsValid, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);
            
            AddAssetDependencyInternal(AssetID.GetAssetPath(asset), AssetID.GetAssetPath(dependency), density);
        }

        /// <summary>
        /// Clears all of the registered dependencies for the specified asset.
        /// </summary>
        /// <param name="asset">The asset path of the asset for which to clear dependencies.</param>
        public void ClearAssetDependencies(String asset)
        {
            Contract.Require(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
            {
                if (assetDependencies == null)
                    return;
                
                foreach (var dependents in assetDependencies)
                    dependents.Value.DependentAssets.Remove(asset);
            }
        }

        /// <summary>
        /// Clears all of the registered dependencies for the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to clear dependencies.</param>
        public void ClearAssetDependencies(AssetID asset)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            ClearAssetDependencies(AssetID.GetAssetPath(asset));
        }

        /// <summary>
        /// Clears all of the registered dependencies for this content manager.
        /// </summary>
        public void ClearAssetDependencies()
        {
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
                assetDependencies?.Clear();
        }

        /// <summary>
        /// Removes the specified dependency from an asset.
        /// </summary>
        /// <param name="asset">The asset path of the asset from which to remove a dependency.</param>
        /// <param name="dependency">The asset path of the dependency to remove from the specified asset.</param>
        /// <returns></returns>
        public Boolean RemoveAssetDependency(String asset, String dependency)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return RemoveAssetDependencyInternal(asset, dependency, primaryDisplayDensity);
        }

        /// <summary>
        /// Removes the specified dependency from an asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the asset for which to remove a dependency.</param>
        /// <param name="dependency">The asset identifier of the dependency to remove from the specified asset.</param>
        public Boolean RemoveAssetDependency(AssetID asset, AssetID dependency)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.Ensure<ArgumentException>(dependency.IsValid, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return RemoveAssetDependencyInternal(AssetID.GetAssetPath(asset), AssetID.GetAssetPath(dependency), primaryDisplayDensity);
        }
        
        /// <summary>
        /// Gets a value indicating whether the specified asset is registered as a dependency of another asset.
        /// </summary>
        /// <param name="asset">The asset path of the main asset to evaluate.</param>
        /// <param name="dependency">The asset path of the dependency asset to evaluate.</param>
        /// <returns><see langword="true"/> if <paramref name="dependency"/> is a dependency of <paramref name="asset"/>; otherwise, <see langword="false"/>.</returns>
        public Boolean IsAssetDependency(String asset, String dependency)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);
            
            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return IsAssetDependencyInternal(asset, dependency, primaryDisplayDensity);
        }

        /// <summary>
        /// Gets a value indicating whether the specified asset is registered as a dependency of another asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the main asset to evaluate.</param>
        /// <param name="dependency">The asset identifier of the dependency asset to evaluate.</param>
        /// <returns><see langword="true"/> if <paramref name="dependency"/> is a dependency of <paramref name="asset"/>; otherwise, <see langword="false"/>.</returns>
        public Boolean IsAssetDependency(AssetID asset, AssetID dependency)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.Ensure<ArgumentException>(dependency.IsValid, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);
            
            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return IsAssetDependencyInternal(AssetID.GetAssetPath(asset), AssetID.GetAssetPath(dependency), primaryDisplayDensity);
        }

        /// <summary>
        /// Gets a value indicating whether the specified asset is registered as a dependency of another asset.
        /// </summary>
        /// <param name="asset">The asset path of the main asset to evaluate.</param>
        /// <param name="dependency">The asset path of the dependency asset to evaluate.</param>
        /// <param name="density">The screen density for which to query dependency relationships.</param>
        /// <returns><see langword="true"/> if <paramref name="dependency"/> is a dependency of <paramref name="asset"/>; otherwise, <see langword="false"/>.</returns>
        public Boolean IsAssetDependency(String asset, String dependency, ScreenDensityBucket density)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);
            
            return IsAssetDependencyInternal(asset, dependency, density);
        }

        /// <summary>
        /// Gets a value indicating whether the specified asset is registered as a dependency of another asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the main asset to evaluate.</param>
        /// <param name="dependency">The asset identifier of the dependency asset to evaluate.</param>
        /// <param name="density">The screen density for which to query dependency relationships.</param>
        /// <returns><see langword="true"/> if <paramref name="dependency"/> is a dependency of <paramref name="asset"/>; otherwise, <see langword="false"/>.</returns>
        public Boolean IsAssetDependency(AssetID asset, AssetID dependency, ScreenDensityBucket density)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.Ensure<ArgumentException>(dependency.IsValid, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);
            
            return IsAssetDependencyInternal(AssetID.GetAssetPath(asset), AssetID.GetAssetPath(dependency), density);
        }

        /// <summary>
        /// Gets a value indicating whether the specified file path is registered as a dependency of another asset.
        /// </summary>
        /// <param name="asset">The asset path of the main asset to evaluate.</param>
        /// <param name="dependency">The file path of the dependency asset to evaluate.</param>
        /// <returns><see langword="true"/> if <paramref name="dependency"/> is a dependency of <paramref name="asset"/>; otherwise, <see langword="false"/>.</returns>
        public Boolean IsAssetDependencyPath(String asset, String dependency)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return IsAssetDependencyPathInternal(asset, dependency, primaryDisplayDensity);
        }

        /// <summary>
        /// Gets a value indicating whether the specified asset is registered as a dependency of another asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the main asset to evaluate.</param>
        /// <param name="dependency">The file path of the dependency asset to evaluate.</param>
        /// <returns><see langword="true"/> if <paramref name="dependency"/> is a dependency of <paramref name="asset"/>; otherwise, <see langword="false"/>.</returns>
        public Boolean IsAssetDependencyPath(AssetID asset, String dependency)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return IsAssetDependencyPathInternal(AssetID.GetAssetPath(asset), dependency, primaryDisplayDensity);
        }

        /// <summary>
        /// Gets a value indicating whether the specified file path is registered as a dependency of another asset.
        /// </summary>
        /// <param name="asset">The asset path of the main asset to evaluate.</param>
        /// <param name="dependency">The file path of the dependency asset to evaluate.</param>
        /// <param name="density">The screen density for which to query dependency relationships.</param>
        /// <returns><see langword="true"/> if <paramref name="dependency"/> is a dependency of <paramref name="asset"/>; otherwise, <see langword="false"/>.</returns>
        public Boolean IsAssetDependencyPath(String asset, String dependency, ScreenDensityBucket density)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);
            
            return IsAssetDependencyPathInternal(asset, dependency, density);
        }

        /// <summary>
        /// Gets a value indicating whether the specified asset is registered as a dependency of another asset.
        /// </summary>
        /// <param name="asset">The asset identifier of the main asset to evaluate.</param>
        /// <param name="dependency">The file path of the dependency asset to evaluate.</param>
        /// <param name="density">The screen density for which to query dependency relationships.</param>
        /// <returns><see langword="true"/> if <paramref name="dependency"/> is a dependency of <paramref name="asset"/>; otherwise, <see langword="false"/>.</returns>
        public Boolean IsAssetDependencyPath(AssetID asset, String dependency, ScreenDensityBucket density)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.Require(dependency, nameof(dependency));
            Contract.EnsureNotDisposed(this, Disposed);
            
            return IsAssetDependencyPathInternal(AssetID.GetAssetPath(asset), dependency, density);
        }

        /// <summary>
        /// Gets a <see cref="WatchedAsset{T}"/> which watches the specified asset. The <see cref="WatchedAsset{T}"/> which is returned
        /// is owned by the content manager and shared between all callers of this method. If the watched asset has not already been
        /// loaded, it will be loaded and added to the content manager's internal cache.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <param name="asset">The path to the asset to load.</param>
        /// <returns>The <see cref="WatchedAsset{T}"/> instance which this content manager uses to watch the specified asset.</returns>
        public WatchedAsset<TOutput> GetSharedWatchedAsset<TOutput>(String asset)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay.DensityBucket;

            return GetSharedWatchedAssetInternal<TOutput>(asset, primaryDisplayDensity);
        }

        /// <summary>
        /// Gets a <see cref="WatchedAsset{T}"/> which watches the specified asset. The <see cref="WatchedAsset{T}"/> which is returned
        /// is owned by the content manager and shared between all callers of this method. If the watched asset has not already been
        /// loaded, it will be loaded and added to the content manager's internal cache.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <param name="asset">The identifier of the asset to load.</param>
        /// <returns>The <see cref="WatchedAsset{T}"/> instance which this content manager uses to watch the specified asset.</returns>
        public WatchedAsset<TOutput> GetSharedWatchedAsset<TOutput>(AssetID asset)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay.DensityBucket;

            return GetSharedWatchedAssetInternal<TOutput>(AssetID.GetAssetPath(asset), primaryDisplayDensity);
        }

        /// <summary>
        /// Gets a <see cref="WatchedAsset{T}"/> which watches the specified asset. The <see cref="WatchedAsset{T}"/> which is returned
        /// is owned by the content manager and shared between all callers of this method. If the watched asset has not already been
        /// loaded, it will be loaded and added to the content manager's internal cache.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <param name="asset">The path to the asset to load.</param>
        /// <param name="density">The screen density for which to retrieve an asset watcher.</param>
        /// <returns>The <see cref="WatchedAsset{T}"/> instance which this content manager uses to watch the specified asset.</returns>
        public WatchedAsset<TOutput> GetSharedWatchedAsset<TOutput>(String asset, ScreenDensityBucket density)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            return GetSharedWatchedAssetInternal<TOutput>(asset, density);
        }

        /// <summary>
        /// Gets a <see cref="WatchedAsset{T}"/> which watches the specified asset. The <see cref="WatchedAsset{T}"/> which is returned
        /// is owned by the content manager and shared between all callers of this method. If the watched asset has not already been
        /// loaded, it will be loaded and added to the content manager's internal cache.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <param name="asset">The identifier of the asset to load.</param>
        /// <param name="density">The screen density for which to retrieve an asset watcher.</param>
        /// <returns>The <see cref="WatchedAsset{T}"/> instance which this content manager uses to watch the specified asset.</returns>
        public WatchedAsset<TOutput> GetSharedWatchedAsset<TOutput>(AssetID asset, ScreenDensityBucket density)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            return GetSharedWatchedAssetInternal<TOutput>(AssetID.GetAssetPath(asset), density);
        }

        /// <summary>
        /// Loads all of the assets in the specified <see cref="ContentManifest"/> into the content manager's asset cache.
        /// </summary>
        /// <param name="manifest">The <see cref="ContentManifest"/> to load.</param>
        public void Load(ContentManifest manifest)
        {
            Contract.Require(manifest, nameof(manifest));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDpi = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            foreach (var group in manifest)
            {
                foreach (var asset in group)
                {
                    LoadInternal(asset.AbsolutePath, asset.Type, true, false, primaryDisplayDpi, out var result);
                }
            }
        }

        /// <summary>
        /// Loads the specified asset file.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <remarks>Content managers maintain a cache of references to all loaded assets, so calling Load() multiple
        /// times on a content manager with the same parameter will return the same object rather than reloading the source file.</remarks>
        /// <param name="asset">The path to the asset to load.</param>
        /// <param name="cache">A value indicating whether to add the asset to the manager's cache.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The asset that was loaded from the specified file.</returns>
        public TOutput Load<TOutput>(String asset, Boolean cache = true, Boolean fromsln = false)
        {
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return (TOutput)LoadImpl(asset, typeof(TOutput), primaryDisplayDensity, cache, fromsln);
        }

        /// <summary>
        /// Loads the specified asset file.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <remarks>Content managers maintain a cache of references to all loaded assets, so calling Load() multiple
        /// times on a content manager with the same parameter will return the same object rather than reloading the source file.</remarks>
        /// <param name="asset">The path to the asset to load.</param>
        /// <param name="cache">A value indicating whether to add the asset to the manager's cache.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The asset that was loaded from the specified file.</returns>
        public TOutput Load<TOutput>(AssetID asset, Boolean cache = true, Boolean fromsln = false)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return (TOutput)LoadImpl(AssetID.GetAssetPath(asset), typeof(TOutput), primaryDisplayDensity, cache, fromsln);
        }

        /// <summary>
        /// Loads the specified asset file.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <remarks>Content managers maintain a cache of references to all loaded assets, so calling Load() multiple
        /// times on a content manager with the same parameter will return the same object rather than reloading the source file.</remarks>
        /// <param name="asset">The path to the asset to load.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <param name="cache">A value indicating whether to add the asset to the manager's cache.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The asset that was loaded from the specified file.</returns>
        public TOutput Load<TOutput>(String asset, ScreenDensityBucket density, Boolean cache = true, Boolean fromsln = false)
        {
            Contract.EnsureNotDisposed(this, Disposed);
            
            return (TOutput)LoadImpl(asset, typeof(TOutput), density, cache, fromsln);
        }

        /// <summary>
        /// Loads the specified asset file.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <remarks>Content managers maintain a cache of references to all loaded assets, so calling Load() multiple
        /// times on a content manager with the same parameter will return the same object rather than reloading the source file.</remarks>
        /// <param name="asset">The path to the asset to load.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <param name="cache">A value indicating whether to add the asset to the manager's cache.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The asset that was loaded from the specified file.</returns>
        public TOutput Load<TOutput>(AssetID asset, ScreenDensityBucket density, Boolean cache = true, Boolean fromsln = false)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);
            
            return (TOutput)LoadImpl(AssetID.GetAssetPath(asset), typeof(TOutput), density, cache, fromsln);
        }

        /// <summary>
        /// Loads the specified asset stream.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> that contains the asset to load.</param>
        /// <param name="extension">The file extension to use to search for a content importer.</param>
        /// <returns>The asset that was loaded from the specified stream.</returns>
        public TOutput LoadFromStream<TOutput>(Stream stream, String extension)
        {
            Contract.Require(stream, nameof(stream));
            Contract.RequireNotEmpty(extension, nameof(extension));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay.DensityBucket;

            return (TOutput)LoadInternalFromStream(typeof(TOutput), stream, extension, primaryDisplayDensity);
        }

        /// <summary>
        /// Loads the specified asset stream.
        /// </summary>
        /// <typeparam name="TOutput">The type of object being loaded.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> that contains the asset to load.</param>
        /// <param name="extension">The file extension to use to search for a content importer.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <returns>The asset that was loaded from the specified stream.</returns>
        public TOutput LoadFromStream<TOutput>(Stream stream, String extension, ScreenDensityBucket density)
        {
            Contract.Require(stream, nameof(stream));
            Contract.RequireNotEmpty(extension, nameof(extension));
            Contract.EnsureNotDisposed(this, Disposed);

            return (TOutput)LoadInternalFromStream(typeof(TOutput), stream, extension, density);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="paths">An array of parts of the path to the asset to import.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(params String[] paths)
        {
            Contract.Require(paths, nameof(paths));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            var outputType = typeof(TOutput);
            return (TOutput)ImportInternal(Path.Combine(paths), primaryDisplayDensity, false, ref outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(String asset, Boolean fromsln = false)
        {
            Contract.Require(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            var outputType = typeof(TOutput);
            return (TOutput)ImportInternal(asset, primaryDisplayDensity, fromsln, ref outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="outputType">The output type of the content importer which was used.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(String asset, out Type outputType)
        {
            Contract.Require(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            outputType = typeof(TOutput);
            return (TOutput)ImportInternal(asset, primaryDisplayDensity, false, ref outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <param name="outputType">The output type of the content importer which was used.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(String asset, Boolean fromsln, out Type outputType)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            outputType = typeof(TOutput);
            return (TOutput)ImportInternal(asset, primaryDisplayDensity, fromsln, ref outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(String asset, ScreenDensityBucket density, Boolean fromsln = false)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var outputType = typeof(TOutput);
            return (TOutput)ImportInternal(asset, density, false, ref outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <param name="outputType">The output type of the content importer which was used.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(String asset, ScreenDensityBucket density, out Type outputType)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            outputType = typeof(TOutput);
            return (TOutput)ImportInternal(asset, density, false, ref outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <param name="outputType">The output type of the content importer which was used.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(String asset, ScreenDensityBucket density, Boolean fromsln, out Type outputType)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            outputType = typeof(TOutput);
            return (TOutput)ImportInternal(asset, density, fromsln, ref outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(AssetID asset, Boolean fromsln = false)
        {
            return Import<TOutput>(asset, fromsln, out var outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="outputType">The output type of the content importer which was used.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(AssetID asset, out Type outputType)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));

            return Import<TOutput>(AssetID.GetAssetPath(asset), false, out outputType);
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="asset">The path to the asset to import.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <param name="outputType">The output type of the content importer which was used.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput Import<TOutput>(AssetID asset, Boolean fromsln, out Type outputType)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));

            return Import<TOutput>(AssetID.GetAssetPath(asset), fromsln, out outputType);
        }

        /// <summary>
        /// Imports the specified asset from the specified stream, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> that contains the asset data.</param>
        /// <param name="extension">The file extension to use to search for a content importer.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput ImportFromStream<TOutput>(Stream stream, String extension)
        {
            Contract.Require(stream, nameof(stream));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay.DensityBucket;

            var filename = GetStreamFakeFilename(extension);
            var metadata = CreateMetadataFromStream(ref stream, filename, null, false, true, false, primaryDisplayDensity);
            
            var importerOutputType = typeof(TOutput);
            var importer = FindContentImporter(metadata.AssetFileName, ref importerOutputType);

            return (TOutput)ImportFromStreamOrFile(importer, metadata, stream);
        }

        /// <summary>
        /// Imports the specified asset from the specified stream, but does not process it.
        /// </summary>
        /// <typeparam name="TOutput">The type of the intermediate object produced by the content importer.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> that contains the asset data.</param>
        /// <param name="extension">The file extension to use to search for a content importer.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <returns>The imported asset in its intermediate form.</returns>
        public TOutput ImportFromStream<TOutput>(Stream stream, String extension, ScreenDensityBucket density)
        {
            Contract.Require(stream, nameof(stream));
            Contract.EnsureNotDisposed(this, Disposed);

            var filename = GetStreamFakeFilename(extension);
            var metadata = CreateMetadataFromStream(ref stream, filename, null, false, true, false, density);

            var importerOutputType = typeof(TOutput);
            var importer = FindContentImporter(metadata.AssetFileName, ref importerOutputType);

            return (TOutput)ImportFromStreamOrFile(importer, metadata, stream);
        }

        /// <summary>
        /// Processes an intermediate object into an asset object.
        /// </summary>
        /// <typeparam name="TOutput">The type of the asset object produced by the content processor.</typeparam>
        /// <param name="intermediate">The intermediate object to process.</param>
        /// <param name="metadata">The processor metadata, if any.</param>
        /// <returns>The processed asset.</returns>
        public TOutput Process<TOutput>(Object intermediate, XElement metadata = null)
        {
            Contract.Require(intermediate, nameof(intermediate));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay.DensityBucket;

            var processor = FindContentProcessor("unknown", intermediate.GetType(), typeof(TOutput));
            var assetmeta = (metadata == null) ? AssetMetadata.CreateInMemoryMetadata(primaryDisplayDensity) : 
                new AssetMetadata(null, null, null, null, metadata, false, false, false, false, primaryDisplayDensity);
            var result = processor.Process(this, assetmeta, intermediate);

            return (TOutput)result;
        }

        /// <summary>
        /// Processes an intermediate object into an asset object.
        /// </summary>
        /// <typeparam name="TInput">The type of the intermediate object being processed.</typeparam>
        /// <typeparam name="TOutput">The type of the asset object produced by the content processor.</typeparam>
        /// <param name="intermediate">The intermediate object to process.</param>
        /// <param name="metadata">The processor metadata, if any.</param>
        /// <returns>The processed asset.</returns>
        public TOutput Process<TInput, TOutput>(TInput intermediate, XElement metadata = null) where TInput : class
        {
            Contract.Require(intermediate, nameof(intermediate));
            Contract.EnsureNotDisposed(this, Disposed);

            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay.DensityBucket;

            var processor = FindContentProcessor("unknown", typeof(TInput), typeof(TOutput));
            var assetmeta = (metadata == null) ? AssetMetadata.CreateInMemoryMetadata(primaryDisplayDensity) : 
                new AssetMetadata(null, null, null, null, metadata, false, false, false, false, primaryDisplayDensity);
            var result = processor.Process(this, assetmeta, intermediate);

            return (TOutput)result;
        }

        /// <summary>
        /// Processes an intermediate object into an asset object.
        /// </summary>
        /// <typeparam name="TOutput">The type of the asset object produced by the content processor.</typeparam>
        /// <param name="intermediate">The intermediate object to process.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <param name="metadata">The processor metadata, if any.</param>
        /// <returns>The processed asset.</returns>
        public TOutput Process<TOutput>(Object intermediate, ScreenDensityBucket density, XElement metadata = null)
        {
            Contract.Require(intermediate, nameof(intermediate));
            Contract.EnsureNotDisposed(this, Disposed);
            
            var processor = FindContentProcessor("unknown", intermediate.GetType(), typeof(TOutput));
            var assetmeta = (metadata == null) ? AssetMetadata.CreateInMemoryMetadata(density) :
                new AssetMetadata(null, null, null, null, metadata, false, false, false, false, density);
            var result = processor.Process(this, assetmeta, intermediate);

            return (TOutput)result;
        }

        /// <summary>
        /// Processes an intermediate object into an asset object.
        /// </summary>
        /// <typeparam name="TInput">The type of the intermediate object being processed.</typeparam>
        /// <typeparam name="TOutput">The type of the asset object produced by the content processor.</typeparam>
        /// <param name="intermediate">The intermediate object to process.</param>
        /// <param name="density">The density bucket for which to load the asset.</param>
        /// <param name="metadata">The processor metadata, if any.</param>
        /// <returns>The processed asset.</returns>
        public TOutput Process<TInput, TOutput>(TInput intermediate, ScreenDensityBucket density, XElement metadata = null) where TInput : class
        {
            Contract.Require(intermediate, nameof(intermediate));
            Contract.EnsureNotDisposed(this, Disposed);
            
            var processor = FindContentProcessor("unknown", typeof(TInput), typeof(TOutput));
            var assetmeta = (metadata == null) ? AssetMetadata.CreateInMemoryMetadata(density) :
                new AssetMetadata(null, null, null, null, metadata, false, false, false, false, density);
            var result = processor.Process(this, assetmeta, intermediate);

            return (TOutput)result;
        }

        /// <summary>
        /// Preprocesses the assets in the specified content manifest.
        /// </summary>
        /// <param name="manifest">The content manifest to preprocess.</param>
        /// <param name="delete">A value indicating whether to delete the original files after preprocessing them.</param>
        public void Preprocess(ContentManifest manifest, Boolean delete = false)
        {
            Contract.Require(manifest, nameof(manifest));
            Contract.EnsureNotDisposed(this, Disposed);

            PreprocessInternal(new[] { manifest }, delete);
        }

        /// <summary>
        /// Preprocesses the assets in the specified content manifests.
        /// </summary>
        /// <param name="manifests">A collection containing the content manifests to preprocess.</param>
        /// <param name="delete">A value indicating whether to delete the original files after preprocessing them.</param>
        public void Preprocess(IEnumerable<ContentManifest> manifests, Boolean delete = false)
        {
            Contract.Require(manifests, nameof(manifests));
            Contract.EnsureNotDisposed(this, Disposed);

            PreprocessInternal(manifests, delete);
        }

        /// <summary>
        /// Preprocesses the specified asset by saving it in a binary format designed for fast deserialization.
        /// If the asset's content importer does not support a binary data format, this method has no effect.
        /// </summary>
        /// <typeparam name="TOutput">The type of asset to preprocess.</typeparam>
        /// <param name="asset">The asset to preprocess.</param>
        /// <param name="delete">A value indicating whether to delete the original file after preprocessing it.</param>
        /// <returns><see langword="true"/> if the asset was preprocessed; otherwise, <see langword="false"/>.</returns>
        public Boolean Preprocess<TOutput>(String asset, Boolean delete = false)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            return PreprocessInternal(asset, typeof(TOutput), delete);
        }

        /// <summary>
        /// Preprocesses the specified asset by saving it in a binary format designed for fast deserialization.
        /// If the asset's content importer does not support a binary data format, this method has no effect.
        /// </summary>
        /// <typeparam name="TOutput">The type of asset to preprocess.</typeparam>
        /// <param name="asset">The asset to preprocess.</param>
        /// <param name="delete">A value indicating whether to delete the original file after preprocessing it.</param>
        /// <returns><see langword="true"/> if the asset was preprocessed; otherwise, <see langword="false"/>.</returns>
        public Boolean Preprocess<TOutput>(AssetID asset, Boolean delete = false)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));

            return PreprocessInternal(AssetID.GetAssetPath(asset), typeof(TOutput), delete);
        }

        /// <summary>
        /// Flushes the file deletion buffer.
        /// </summary>
        public void FlushDeletedFiles()
        {
            Contract.Ensure(batchDeletedFiles, UltravioletStrings.ContentManagerNotBatchingDeletes);

            foreach (var file in filesPendingDeletion)
            {
                File.Delete(file);
            }
            filesPendingDeletion.Clear();
        }

        /// <summary>
        /// Resolves the full path to the file that represents the specified asset.
        /// </summary>
        /// <param name="asset">The asset path for which to resolve a file path.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The full path to the file that represents the specified asset.</returns>
        public String ResolveAssetFilePath(String asset, Boolean fromsln = false)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var metadata = GetAssetMetadata(NormalizeAssetPath(asset), true, false, fromsln);
            if (metadata == null)
                throw new FileNotFoundException(asset);

            return fileSystemService.GetFullPath(metadata.AssetFilePath);
        }

        /// <summary>
        /// Resolves the full path to the file that represents the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier for which to resolve a file path.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The full path to the file that represents the specified asset.</returns>
        public String ResolveAssetFilePath(AssetID asset, Boolean fromsln = false)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var assetPath = AssetID.GetAssetPath(asset);

            var metadata = GetAssetMetadata(NormalizeAssetPath(assetPath), true, false, fromsln);
            if (metadata == null)
                throw new FileNotFoundException(assetPath);

            return fileSystemService.GetFullPath(metadata.AssetFilePath);
        }

        /// <summary>
        /// Resolves the full path to the file that represents the specified asset.
        /// </summary>
        /// <param name="asset">The asset path for which to resolve a file path.</param>
        /// <param name="density">The density bucket corresponding to the file to retrieve.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The full path to the file that represents the specified asset.</returns>
        public String ResolveAssetFilePath(String asset, ScreenDensityBucket density, Boolean fromsln = false)
        {
            Contract.RequireNotEmpty(asset, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var metadata = GetAssetMetadata(NormalizeAssetPath(asset), density, true, false, fromsln);
            if (metadata == null)
                throw new FileNotFoundException(asset);

            return fileSystemService.GetFullPath(metadata.AssetFilePath);
        }

        /// <summary>
        /// Resolves the full path to the file that represents the specified asset.
        /// </summary>
        /// <param name="asset">The asset identifier for which to resolve a file path.</param>
        /// <param name="density">The density bucket corresponding to the file to retrieve.</param>
        /// <param name="fromsln">A value indicating whether asset resolution should search the Visual Studio solution
        /// directory, rather than the directory containing the application binaries. This is useful primarily for reloading
        /// assets while the application is being debugged, and should mostly be avoided unless you know what you're doing.</param>
        /// <returns>The full path to the file that represents the specified asset.</returns>
        public String ResolveAssetFilePath(AssetID asset, ScreenDensityBucket density, Boolean fromsln = false)
        {
            Contract.Ensure<ArgumentException>(asset.IsValid, nameof(asset));
            Contract.EnsureNotDisposed(this, Disposed);

            var assetPath = AssetID.GetAssetPath(asset);

            var metadata = GetAssetMetadata(NormalizeAssetPath(assetPath), density, true, false, fromsln);
            if (metadata == null)
                throw new FileNotFoundException(assetPath);

            return fileSystemService.GetFullPath(metadata.AssetFilePath);
        }

        /// <summary>
        /// Gets a list of assets in the specified asset directory.
        /// </summary>
        /// <param name="path">The directory to evaluate.</param>
        /// <param name="searchPattern">The search string to match against the names of files in the path.</param>
        /// <returns>The list of assets in the specified asset directory.</returns>
        public IEnumerable<String> GetAssetsInDirectory(String path, String searchPattern = "*")
        {
            Contract.RequireNotEmpty(path, nameof(path));
            Contract.RequireNotEmpty(searchPattern, nameof(searchPattern));
            Contract.EnsureNotDisposed(this, Disposed);

            return GetAssetsInDirectoryInternal(NormalizeAssetPath(path), searchPattern).Keys;
        }

        /// <summary>
        /// Gets a list of resolved asset file paths in the specified asset directory.
        /// </summary>
        /// <param name="path">The directory to evaluate.</param>
        /// <param name="searchPattern">The search string to match against the names of files in the path.</param>
        /// <returns>The list of resolved asset file paths in the specified asset directory.</returns>
        public IEnumerable<String> GetAssetFilePathsInDirectory(String path, String searchPattern = "*")
        {
            Contract.RequireNotEmpty(path, nameof(path));
            Contract.RequireNotEmpty(searchPattern, nameof(searchPattern));
            Contract.EnsureNotDisposed(this, Disposed);

            return GetAssetsInDirectoryInternal(NormalizeAssetPath(path), searchPattern).Values;
        }

        /// <summary>
        /// Gets a list of subdirectories in the specified asset directory.
        /// </summary>
        /// <param name="path">The directory to evaluate.</param>
        /// <param name="searchPattern">The search string to match against the names of directories in the path.</param>
        /// <returns>The list of subdirectories in the specified asset directory.</returns>
        public IEnumerable<String> GetSubdirectories(String path, String searchPattern = "*")
        {
            Contract.Require(path, nameof(path));
            Contract.EnsureNotDisposed(this, Disposed);

            var results = new Dictionary<String, String>();

            GetSubdirectories(RootDirectory, path, searchPattern, results);

            if (!Path.IsPathRooted(path))
            {
                foreach (var dir in overrideDirectories)
                {
                    GetSubdirectories(dir, path, searchPattern, results);
                }
            }

            return results.Keys;
        }

        /// <summary>
        /// Gets a value indicating whether watched content is supported on the current platform.
        /// </summary>
        public static Boolean IsWatchedContentSupported
        {
            get
            {
                var platform = UltravioletPlatformInfo.CurrentPlatform;
                return (platform != UltravioletPlatform.Android && platform != UltravioletPlatform.iOS);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether dependency tracking is globally suppressed, reducing memory usage.
        /// As a side effect, modifying a dependency of an asset will no longer cause the parent asset to be reloaded
        /// in an application which makes use of watched assets.
        /// </summary>
        /// <remarks>Setting this property to <see langword="true"/> will not purge any existing dependency tracking caches,
        /// so if you turn this on, be sure to do it before loading any content assets.</remarks>
        public static Boolean GloballySuppressDependencyTracking { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether dependency tracking is suppressed for this content manager, reducing memory usage.
        /// As a side effect, modifying a dependency of an asset will no longer cause the parent asset to be reloaded
        /// in an application which makes use of watched assets.
        /// </summary>
        /// <remarks>Setting this property to <see langword="true"/> will not purge any existing dependency tracking caches,
        /// so if you turn this on, be sure to do it before loading any content assets.</remarks>
        public Boolean SuppressDependencyTracking { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the content manager should batch file deletions.
        /// </summary>
        /// <remarks>When this property is set to <see langword="true"/>, <see cref="ContentManager"/> will not delete files
        /// immediately. Instead, it will buffer deletions until the batch is ended. This is useful when, for example,
        /// preprocessing a large number of files which depend on the same raw resources; batching deletes ensures that
        /// those raw resources remain on disk until all of the assets are preprocessed.</remarks>
        public Boolean BatchDeletedFiles
        {
            get => batchDeletedFiles;
            set
            {
                Contract.EnsureNotDisposed(this, Disposed);
                Contract.EnsureNot(batchDeletedFilesGuarantee, UltravioletStrings.ContentManagerRequiresBatch);

                if (batchDeletedFiles)
                {
                    FlushDeletedFiles();
                }
                batchDeletedFiles = value;
            }
        }

        /// <summary>
        /// Gets the content manager's root directory.
        /// </summary>
        public String RootDirectory { get; }

        /// <summary>
        /// Gets the full path to the content manager's root directory.
        /// </summary>
        public String FullRootDirectory { get; }

        /// <summary>
        /// Gets the content manager's collection of override directories.
        /// </summary>
        /// <remarks>Override directories are alternative paths where the content manager will search for asset files.
        /// If multiple override directories contain an asset with the same path, directories which have higher indices 
        /// within this collection will take priority over directories with lower indices, "overriding" the other values
        /// of the asset in question. All override directories take precedence over the content manager's root directory.</remarks>
        public ContentOverrideDirectoryCollection OverrideDirectories
        {
            get
            {
                Contract.EnsureNotDisposed(this, Disposed);

                return overrideDirectories;
            }
        }

        /// <summary>
        /// Implements the <see cref="Load"/> method.
        /// </summary>
        internal Object LoadImpl(String asset, Type type, ScreenDensityBucket density, Boolean cache, Boolean fromsln)
        {
            return LoadImpl(asset, type, density, cache, fromsln, null, null);
        }
        
        /// <summary>
        /// Implements the <see cref="Load"/> method.
        /// </summary>
        internal Object LoadImpl(String asset, Type type, ScreenDensityBucket density, Boolean cache, Boolean fromsln, IAssetWatcherCollection watchers, Object lastKnownGood)
        {
            var cachedInstanceEntry = default(AssetCacheEntry);
            var cacheMiss = false;

            lock (cacheSyncObject)
                cacheMiss = !assetCache.TryGetValue(asset, out cachedInstanceEntry);
            
            if (cacheMiss)
            {
                LoadInternal(asset, type, cache, fromsln, density, watchers, lastKnownGood, out var result);
                return result;
            }
            else
            {
                if (cachedInstanceEntry.GetVersion(density, out var instance))
                    return instance;
                
                LoadInternal(asset, type, cache, fromsln, density, watchers, lastKnownGood, out var result);
                return result;
            }
        }
        
        /// <summary>
        /// Called when a change in the file system is detected.
        /// </summary>
        internal void OnFileSystemChanged(Object sender, FileSystemEventArgs e)
        {
            OnFileReloaded(e.FullPath);
        }

        /// <summary>
        /// Called when a change in a file is detected.
        /// </summary>
        internal void OnFileReloaded(String fullPath)
        {
            var assetWatchersForFile = default(IAssetWatcherCollection);
            var assetDependenciesForFile = default(IAssetDependencyCollection);

            assetWatchers?.TryGetValue(fullPath, out assetWatchersForFile);
            assetDependencies?.TryGetValue(fullPath, out assetDependenciesForFile);
            
            // Reload the file if it already exists in our cache
            var assetPath = assetWatchersForFile?.AssetPath ?? assetDependenciesForFile?.AssetPath;
            if (assetPath != null && assetCache.TryGetValue(assetPath, out var assetEntry))
            {
                Ultraviolet.QueueWorkItem(state =>
                {
                    assetEntry.Reload(this, assetPath, assetWatchersForFile);
                });
            }

            // Inform the file's dependent assets that it changed
            if (assetDependenciesForFile != null)
            {
                foreach (var dependent in assetDependenciesForFile.DependentAssets)
                {
                    var dependentFullPath = Path.GetFullPath(ResolveAssetFilePath(dependent, true));
                    OnFileReloaded(dependentFullPath);
                }
            }
        }

        /// <summary>
        /// Releases resources associated with this object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if the object is being disposed; <see langword="false"/> if the object is being finalized.</param>
        protected override void Dispose(Boolean disposing)
        {
            if (Disposed)
                return;

            if (disposing)
            {
                Ultraviolet.Messages.Unsubscribe(this);

                lock (cacheSyncObject)
                {
                    foreach (var instance in assetCache)
                        instance.Value.Dispose();

                    assetCache.Clear();
                    assetFlags.Clear();

                    if (sharedWatchedAssets != null)
                    {
                        foreach (var x in sharedWatchedAssets)
                        {
                            foreach (var y in x.Value)
                                ((IDisposable)y.Value).Dispose();
                        }
                        sharedWatchedAssets.Clear();
                    }

                    if (rootFileSystemWatcher != null)
                        rootFileSystemWatcher.Dispose();

                    OverrideDirectories.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Lists the assets which can serve as substitutions for the specified asset.
        /// </summary>
        private IEnumerable<String> ListPossibleSubstitutions(String rootdir, String path, ScreenDensityBucket minDensityBucket, ScreenDensityBucket maxDensityBucket)
        {
            var directory = Path.GetDirectoryName(path) ?? String.Empty;
            var filename = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            var substitutions =
                from bucket in ScreenDensityBuckets.OrderByDescending(x => x)
                where bucket >= minDensityBucket && bucket <= maxDensityBucket
                let bucketname = ScreenDensityService.GetDensityBucketName(bucket)
                let bucketfile = String.Format("{0}-{1}{2}", filename, bucketname, extension)
                let bucketpath = Path.Combine(directory, bucketfile)
                where fileSystemService.FileExists(bucketpath)
                select fileSystemService.GetRelativePath(rootdir, bucketpath);

            return substitutions;
        }

        /// <summary>
        /// Finds the importer for the specified asset.
        /// </summary>
        /// <param name="asset">The asset for which to find an importer.</param>
        /// <param name="outputType">The importer's output type.</param>
        /// <returns>The importer for the specified asset.</returns>
        private IContentImporter FindContentImporter(String asset, ref Type outputType)
        {
            var extension = Path.GetExtension(asset);
            if (String.IsNullOrEmpty(extension))
            {
                throw new InvalidOperationException(UltravioletStrings.ImporterNeedsExtension.Format(asset));
            }

            var importer = Ultraviolet.GetContent().Importers.FindImporter(extension, ref outputType);
            if (importer == null)
            {
                throw new InvalidOperationException(UltravioletStrings.NoValidImporter.Format(asset));
            }

            return importer;
        }

        /// <summary>
        /// Finds the processor for the specified type.
        /// </summary>
        /// <param name="asset">The asset for which to find a processor.</param>
        /// <param name="inputType">The processor's input type.</param>
        /// <param name="outputType">The processor's output type.</param>
        /// <returns>The processor for the specified type.</returns>
        private IContentProcessor FindContentProcessor(String asset, Type inputType, Type outputType)
        {
            var processor = Ultraviolet.GetContent().Processors.FindProcessor(inputType, outputType);
            if (processor == null)
            {
                if (inputType != outputType)
                    throw new InvalidOperationException(UltravioletStrings.NoValidProcessor.Format(asset));

                return PassthroughContentProcessor.Instance;
            }
            return processor;
        }
        
        /// <summary>
        /// Gets a <see cref="WatchedAsset{T}"/> which watches the specified asset.
        /// </summary>
        private WatchedAsset<TOutput> GetSharedWatchedAssetInternal<TOutput>(String asset, ScreenDensityBucket density)
        {
            lock (cacheSyncObject)
            {
                if (CreateFileSystemWatchers())
                {
                    if (sharedWatchedAssets == null)
                        sharedWatchedAssets = new Dictionary<String, Dictionary<Byte, Object>>();

                    if (!sharedWatchedAssets.TryGetValue(asset, out var watchersForAsset))
                    {
                        watchersForAsset = new Dictionary<Byte, Object>();
                        sharedWatchedAssets[asset] = watchersForAsset;
                    }

                    if (!watchersForAsset.TryGetValue((Byte)density, out var watcherForDensity))
                    {
                        watcherForDensity = new WatchedAsset<TOutput>(this, asset, density);
                        watchersForAsset[(Byte)density] = watcherForDensity;
                    }

                    return (WatchedAsset<TOutput>)watcherForDensity;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a set of file system watchers to monitor the content manager's
        /// file system for changes.
        /// </summary>
        private Boolean CreateFileSystemWatchers()
        {
            if (!IsWatchedContentSupported)
                return false;

            lock (cacheSyncObject)
            {
                if (rootFileSystemWatcher == null)
                {
                    var rootdir = FindSolutionDirectory() ?? RootDirectory;
                    rootFileSystemWatcher = new FileSystemWatcher(rootdir);
                    rootFileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                    rootFileSystemWatcher.IncludeSubdirectories = true;
                    rootFileSystemWatcher.EnableRaisingEvents = true;
                    rootFileSystemWatcher.Changed += OnFileSystemChanged;
                }

                OverrideDirectories.CreateFileSystemWatchers();
            }

            return true;
        }

        /// <summary>
        /// Adds a watcher for the specified asset.
        /// </summary>
        private Boolean AddWatcherInternal<TOutput>(String asset, ScreenDensityBucket density, AssetWatcher<TOutput> watcher)
        {
            lock (cacheSyncObject)
            {
                if (CreateFileSystemWatchers())
                {
                    if (assetWatchers == null)
                        assetWatchers = new Dictionary<String, IAssetWatcherCollection>();

                    var assetPath = asset;
                    var assetFilePath = Path.GetFullPath(ResolveAssetFilePath(assetPath, density, true));

                    if (!assetWatchers.TryGetValue(assetFilePath, out var list))
                    {
                        list = new AssetWatcherCollection<TOutput>(this, assetPath, assetFilePath);
                        assetWatchers[assetFilePath] = list;
                    }

                    list.Add(watcher);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes a watcher from the specified asset.
        /// </summary>
        private Boolean RemoveWatcherInternal<TOutput>(String asset, ScreenDensityBucket density, AssetWatcher<TOutput> watcher)
        {
            Contract.Require(asset, nameof(asset));
            Contract.Require(watcher, nameof(watcher));
            Contract.EnsureNotDisposed(this, Disposed);

            lock (cacheSyncObject)
            {
                if (assetWatchers == null)
                    return false;

                var assetFilePath = ResolveAssetFilePath(asset, density, true);

                if (assetWatchers.TryGetValue(asset, out var list))
                    return list.Remove(watcher);

                return false;
            }
        }

        /// <summary>
        /// Adds the specified dependency to an asset. If the asset is being watched for changes, then any
        /// changes to the specified dependency will also cause the main asset to be reloaded.
        /// </summary>
        private Boolean AddAssetDependencyInternal(String asset, String dependency, ScreenDensityBucket density)
        {
            if (IsDependencyTrackingSuppressed)
                return false;

            var dependencyAssetPath = dependency;
            var dependencyAssetFilePath = Path.GetFullPath(ResolveAssetFilePath(dependencyAssetPath, density, true));

            lock (cacheSyncObject)
            {
                if (assetDependencies == null)
                    assetDependencies = new Dictionary<String, IAssetDependencyCollection>();

                assetDependencies.TryGetValue(dependencyAssetFilePath, out var dependents);

                if (dependents == null)
                {
                    dependents = new AssetDependencyCollection(this, dependencyAssetPath, dependencyAssetFilePath);
                    assetDependencies[dependencyAssetFilePath] = dependents;
                }

                dependents.DependentAssets.Add(asset);
            }

            return true;
        }

        /// <summary>
        /// Removes the specified dependency from an asset.
        /// </summary>
        private Boolean RemoveAssetDependencyInternal(String asset, String dependency, ScreenDensityBucket density)
        {
            var dependencyAssetFilePath = Path.GetFullPath(ResolveAssetFilePath(dependency, density, true));

            lock (cacheSyncObject)
            {
                if (assetDependencies == null)
                    return false;

                if (assetDependencies.TryGetValue(dependencyAssetFilePath, out var dependents))
                    return dependents.DependentAssets.Remove(asset);
            }

            return false;
        }

        /// <summary>
        /// Gets a value indicating whether the specified asset is registered as a dependency of another asset.
        /// </summary>
        private Boolean IsAssetDependencyInternal(String asset, String dependency, ScreenDensityBucket density)
        {
            var dependencyAssetFilePath = Path.GetFullPath(ResolveAssetFilePath(dependency, density, true));

            lock (cacheSyncObject)
            {
                if (assetDependencies == null)
                    return false;

                if (assetDependencies.TryGetValue(dependencyAssetFilePath, out var dependents))
                    return dependents.DependentAssets.Contains(asset);
            }

            return false;
        }

        /// <summary>
        /// Gets a value indicating whether the specified asset is registered as a dependency of another asset.
        /// </summary>
        private Boolean IsAssetDependencyPathInternal(String asset, String dependency, ScreenDensityBucket density)
        {
            var dependencyAssetFilePath = Path.GetFullPath(ResolveAssetFilePath(dependency, density, true));

            lock (cacheSyncObject)
            {
                if (assetDependencies == null)
                    return false;

                if (assetDependencies.TryGetValue(dependencyAssetFilePath, out var dependents))
                    return dependents.DependentAssets.Contains(asset);
            }

            return false;
        }

        /// <summary>
        /// Loads the specified content file.
        /// </summary>
        private Boolean LoadInternal(String asset, Type type, Boolean cache, Boolean fromsln, ScreenDensityBucket density, out Object result)
        {
            return LoadInternal(asset, type, cache, fromsln, density, null, null, out result);
        }
        
        /// <summary>
        /// Loads the specified content file.
        /// </summary>
        private Boolean LoadInternal(String asset, Type type, Boolean cache, Boolean fromsln, ScreenDensityBucket density, IAssetWatcherCollection watchers, Object lastKnownGood, out Object result)
        {
            result = null;

            var normalizedAsset = NormalizeAssetPath(asset);

            var metadata = GetAssetMetadata(normalizedAsset, density, true, true, fromsln);
            var preprocessed = IsPreprocessedFile(metadata.AssetFilePath);
            var importer = default(IContentImporter);
            var processor = default(IContentProcessor);
            var instance = default(Object);
            var changed = true;
            
            try
            {
                instance = preprocessed ?
                    LoadInternalPreprocessed(type, normalizedAsset, metadata.AssetFilePath, metadata.OverrideDirectory, density, out importer, out processor) :
                    LoadInternalRaw(type, normalizedAsset, metadata, density, out importer, out processor);
            }
            catch (Exception e)
            {
                if (watchers == null || watchers.Count == 0 || lastKnownGood == null)
                    throw;

                Debug.WriteLine(UltravioletStrings.ExceptionDuringContentReloading);
                Debug.WriteLine(e);

                instance = lastKnownGood;
                changed = false;
            }

            if (changed)
            {
                if (cache)
                    UpdateCache(asset, metadata, ref instance, type, density);

                if (watchers != null)
                {
                    var validated = true;

                    for (var i = 0; i < watchers.Count; i++)
                    {
                        if (!watchers[i].OnValidating(asset, instance))
                        {
                            if (instance is IDisposable disposable)
                                disposable.Dispose();

                            validated = false;
                            instance = lastKnownGood;

                            if (cache)
                                UpdateCache(asset, metadata, ref instance, type, density);

                            for (int j = 0; j <= i; j++)
                                watchers[i].OnValidationComplete(asset, instance, false);

                            validated = false;
                            changed = false;

                            break;
                        }
                    }

                    if (validated)
                    {
                        for (int i = 0; i < watchers.Count; i++)
                            watchers[i].OnValidationComplete(asset, instance, true);
                    }
                }

                if (changed)
                {
                    ClearAssetDependencies(asset);

                    foreach (var dependency in metadata.AssetDependencies)
                        AddAssetDependency(asset, dependency, density);
                }
            }

            result = instance;
            return true;
        }

        /// <summary>
        /// Imports the contents of the specified stream, if given; otherwise, imports the contents
        /// of the file at the path represented by the specified asset metadata.
        /// </summary>
        private Object ImportFromStreamOrFile(IContentImporter importer, AssetMetadata metadata, Stream stream)
        {
            if (stream != null)
            {
                return importer.Import(metadata, stream);
            }
            else
            {
                using (stream = fileSystemService.OpenRead(metadata.AssetFilePath))
                {
                    return importer.Import(metadata, stream);
                }
            }
        }

        /// <summary>
        /// Imports the specified asset, but does not process it.
        /// </summary>
        private Object ImportInternal(String asset, ScreenDensityBucket density, Boolean fromsln, ref Type outputType)
        {
            var metadata = GetAssetMetadata(asset, density, false, true, fromsln);
            var importer = FindContentImporter(metadata.AssetFileName, ref outputType);

            using (var stream = fileSystemService.OpenRead(metadata.AssetFilePath))
            {
                return importer.Import(metadata, stream);
            }
        }

        /// <summary>
        /// Loads a raw asset from a stream.
        /// </summary>
        private Object LoadInternalFromStream(Type type, Stream stream, String extension, ScreenDensityBucket density)
        {
            var filename = GetStreamFakeFilename(extension);
            var metadata = CreateMetadataFromStream(ref stream, filename, null, false, true, false, density);

            var importerOutputType = type;
            var importer = FindContentImporter(metadata.AssetFileName, ref importerOutputType);
            var intermediate = ImportFromStreamOrFile(importer, metadata, stream);

            try
            {
                if (intermediate == null)
                    throw new InvalidOperationException(UltravioletStrings.ImporterOutputInvalid.Format(filename));

                var processor = FindContentProcessor(metadata.AssetFileName, importerOutputType, type);
                return processor.Process(this, metadata, intermediate);
            }
            finally
            {
                var disposable = intermediate as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
        }

        /// <summary>
        /// Loads a raw asset.
        /// </summary>
        private Object LoadInternalRaw(Type type, String asset, AssetMetadata metadata, ScreenDensityBucket density, out IContentImporter importer, out IContentProcessor processor)
        {
            var importerOutputType = type;
            importer = FindContentImporter(metadata.AssetFileName, ref importerOutputType);

            var intermediate = default(Object);
            try
            {
                using (var stream = fileSystemService.OpenRead(metadata.AssetFilePath))
                {
                    intermediate = importer.Import(metadata, stream);
                    if (intermediate == null)
                        throw new InvalidOperationException(UltravioletStrings.ImporterOutputInvalid.Format(asset));
                }

                processor = FindContentProcessor(metadata.AssetFilePath, importerOutputType, type);

                return processor.Process(this, metadata, intermediate);
            }
            finally
            {
                if (intermediate is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        /// <summary>
        /// Loads a preprocessed asset.
        /// </summary>
        private Object LoadInternalPreprocessed(Type type, String asset, String path, String overridedir, ScreenDensityBucket density, out IContentImporter importer, out IContentProcessor processor)
        {
            importer = null;
            using (var stream = fileSystemService.OpenRead(path))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var uvcHeader = reader.ReadString();
                    if (uvcHeader != "UVC0")
                        throw new InvalidDataException();

                    var uvcProcessorTypeName = reader.ReadString();
                    var uvcProcessorType = Type.GetType(uvcProcessorTypeName);

                    for (var currentType = uvcProcessorType; currentType != null; currentType = currentType.BaseType)
                    {
                        if (currentType.IsGenericType)
                        {
                            var genericTypeDef = currentType.GetGenericTypeDefinition();
                            if (genericTypeDef == typeof(ContentProcessor<,>))
                            {
                                if (!type.IsAssignableFrom(currentType.GetGenericArguments()[1]))
                                    throw new InvalidOperationException(UltravioletStrings.PreprocessedAssetTypeMismatch.Format(path, type.Name));

                                break;
                            }
                        }

                        if (currentType.BaseType == null)
                            throw new InvalidOperationException(UltravioletStrings.ProcessorInvalidBaseClass.Format(uvcProcessorType.FullName));
                    }


                    processor = (IContentProcessor)Activator.CreateInstance(uvcProcessorType);

                    var metadata = new AssetMetadata(overridedir, asset, fileSystemService.GetFullPath(path), null, null, true, false, false, false, density);
                    return processor.ImportPreprocessed(this, metadata, reader);
                }
            }
        }

        /// <summary>
        /// Preprocesses the assets in the specified content manifests.
        /// </summary>
        /// <param name="manifests">A collection containing the content manifests to preprocess.</param>
        /// <param name="delete">A value indicating whether to delete the original files after preprocessing them.</param>
        private void PreprocessInternal(IEnumerable<ContentManifest> manifests, Boolean delete)
        {
            var batch = false;
            if (!BatchDeletedFiles)
            {
                BatchDeletedFiles = true;
                batch = true;
            }
            batchDeletedFilesGuarantee = true;

            foreach (var manifest in manifests)
            {
                foreach (var group in manifest)
                {
                    foreach (var asset in group)
                    {
                        PreprocessInternal(asset.AbsolutePath, asset.Type, delete);
                    }
                }
            }

            if (batch)
            {
                batchDeletedFilesGuarantee = false;
                BatchDeletedFiles = false;
            }
        }

        /// <summary>
        /// Preprocesses an asset.
        /// </summary>
        private Boolean PreprocessInternal(String asset, AssetMetadata metadata, IContentProcessor processor, Object intermediate, Boolean delete)
        {
            if (!processor.SupportsPreprocessing)
                return false;

            using (var stream = File.Open(Path.ChangeExtension(metadata.AssetFilePath, PreprocessedFileExtension), FileMode.Create))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    var processorType = processor.GetType();
                    var processorTypeName = String.Format("{0}, {1}", processorType.FullName, processorType.Assembly.GetName().Name);

                    writer.Write("UVC0");
                    writer.Write(processorTypeName);

                    processor.ExportPreprocessed(this, metadata, writer, intermediate, delete);
                    return true;
                }
            }
        }

        /// <summary>
        /// Preprocesses the specified asset by saving it in a binary format designed for fast deserialization.
        /// If the asset's content importer does not support a binary data format, this method has no effect.
        /// </summary>
        private Boolean PreprocessInternal(String asset, Type type, Boolean delete)
        {
            Object result;

            var assetDirectory = String.Empty;
            var assetOveridden = false;
            var assetPath = GetAssetPath(asset, null, out assetDirectory, out assetOveridden, AssetResolutionFlags.None);

            if (IsPreprocessedFile(assetPath))
                return true;

            var substitutions = ListPossibleSubstitutions(assetDirectory, assetPath, ScreenDensityBucket.Desktop, ScreenDensityBucket.ExtraExtraExtraHigh);
            foreach (var substitution in substitutions)
            {
                if (!PreprocessInternal(substitution, type, delete, false, out result))
                {
                    return false;
                }
            }

            return PreprocessInternal(asset, type, delete, false, out result);
        }

        /// <summary>
        /// Preprocesses the specified asset by saving it in a binary format designed for fast deserialization.
        /// If the asset's content importer does not support a binary data format, this method has no effect.
        /// </summary>
        private Boolean PreprocessInternal(String asset, Type type, Boolean delete, Boolean fromsln, out Object result)
        {
            result = null;

            var normalizedAsset = NormalizeAssetPath(asset);

            var metadata = GetAssetMetadata(normalizedAsset, false, true, fromsln);
            var preprocessed = IsPreprocessedFile(metadata.AssetFilePath);
            if (preprocessed)
                return true;

            var intermediateObjectType = default(Type);
            var intermediate = Import<Object>(normalizedAsset, out intermediateObjectType);
            var processor = FindContentProcessor(normalizedAsset, intermediateObjectType, type);
            var preprocessSucceeded = PreprocessInternal(normalizedAsset, metadata, processor, intermediate, delete);
            if (preprocessSucceeded && delete)
            {
                if (batchDeletedFiles)
                {
                    filesPendingDeletion.Add(metadata.AssetFilePath);
                }
                else
                {
                    File.Delete(metadata.AssetFilePath);
                }
            }
            return preprocessSucceeded;
        }

        /// <summary>
        /// Gets a value indicating whether the specified file is preprocessed.
        /// </summary>
        /// <param name="filename">The filename to evaluate.</param>
        /// <returns><see langword="true"/> if the specified file is preprocessed; otherwise, <see langword="false"/>.</returns>
        private Boolean IsPreprocessedFile(String filename)
        {
            return Path.GetExtension(filename) == PreprocessedFileExtension;
        }

        /// <summary>
        /// Gets a value indicating whether the specified file contains asset metadata.
        /// </summary>
        /// <param name="filename">The filename to evaluate.</param>
        /// <param name="extension">The file extension of the metadata file.</param>
        /// <returns><see langword="true"/> if the specified file contains asset metadata; otherwise, <see langword="false"/>.</returns>
        private Boolean IsMetadataFile(String filename, out String extension)
        {
            extension = Path.GetExtension(filename)?.ToLowerInvariant();
            return
                extension == MetadataFileExtensionXml ||
                extension == MetadataFileExtensionJson;
        }

        /// <summary>
        /// Gets the fake filename which is appears in the metadata object for a stream.
        /// </summary>
        private String GetStreamFakeFilename(String extension)
        {
            return extension?[0] == '.' ? $"__STREAM{extension}" : $"__STREAM.{extension}";
        }

        /// <summary>
        /// Attempts to find the directory that contains the application solution file, if 
        /// we have reason to believe that we're running in debug mode.
        /// </summary>
        /// <returns>The solution directory, if it was found; otherwise, <see langword="null"/>.</returns>
        private String FindSolutionDirectory()
        {
            if (solutionDirectory != null)
                return solutionDirectory == String.Empty ? null : solutionDirectory;

            if (Ultraviolet.Platform == UltravioletPlatform.Android || Ultraviolet.Platform == UltravioletPlatform.iOS)
            {
                solutionDirectory = String.Empty;
                return null;
            }

            var asm = Assembly.GetEntryAssembly();
            if (asm == null)
            {
                solutionDirectory = String.Empty;
                return null;
            }

            var asmDebuggableAttr = (DebuggableAttribute)asm.GetCustomAttributes(typeof(DebuggableAttribute), false).FirstOrDefault();
            if (asmDebuggableAttr == null || !asmDebuggableAttr.IsJITOptimizerDisabled)
            {
                solutionDirectory = String.Empty;
                return null;
            }

            var asmDir = new DirectoryInfo(Path.GetDirectoryName(AppContext.BaseDirectory));
            if (asmDir == null || !asmDir.Exists)
            {
                solutionDirectory = String.Empty;
                return null;
            }

            // Break out of the bin directory. There's a different number of steps depending on our platform.
            var depth = 
                (Ultraviolet.Runtime == UltravioletRuntime.CoreCLR) ? 3 :
                (Ultraviolet.Platform == UltravioletPlatform.macOS) ? 5 : 2;
            for (int i = 0; i < depth; i++)
            {
                asmDir = asmDir.Parent;
                if (asmDir == null || !asmDir.Exists)
                {
                    solutionDirectory = String.Empty;
                    return null;
                }
            }

            // Is there a directory here with the same structure as our content root?
            var projectContentRoot = new DirectoryInfo(Path.Combine(asmDir.FullName, RootDirectory));
            if (projectContentRoot == null || !projectContentRoot.Exists)
            {
                // If we're on a Mac, check for a Desktop version of the project by
                // going up another level and looking for a "Desktop" directory.
                // If your app doesn't follow the Ultraviolet convention here, then
                // unfortunately you're out of luck.
                if (Ultraviolet.Runtime == UltravioletRuntime.Mono && Ultraviolet.Platform == UltravioletPlatform.macOS)
                {
                    asmDir = asmDir.Parent;
                    if (asmDir != null && asmDir.Exists)
                    {
                        var desktopRoot = new DirectoryInfo(Path.Combine(asmDir.FullName, "Desktop"));
                        if (desktopRoot != null && desktopRoot.Exists)
                        {
                            // Check again for the content root...
                            projectContentRoot = new DirectoryInfo(Path.Combine(desktopRoot.FullName, RootDirectory));
                            if (projectContentRoot != null && projectContentRoot.Exists)
                            {
                                solutionDirectory = projectContentRoot.FullName;
                                return solutionDirectory;
                            }
                        }
                    }
                }
                solutionDirectory = String.Empty;
                return null;
            }

            solutionDirectory = projectContentRoot.FullName;
            return solutionDirectory;
        }

        /// <summary>
        /// Gets the path of the specified asset relative to the specified root directory.
        /// </summary>
        /// <param name="root">The root directory.</param>
        /// <param name="asset">The asset name.</param>
        /// <param name="extension">The required file extension, if any; otherwise, <see langword="null"/>.</param>
        /// <param name="flags">A collection of <see cref="AssetResolutionFlags"/> values indicating how to resolve the asset path.</param>
        /// <returns>The path of the specified asset relative to the specified root directory.</returns>
        private String GetAssetPathFromDirectory(String root, String asset, ref String extension, AssetResolutionFlags flags = AssetResolutionFlags.Default)
        {
            var includePreprocessed = (flags & AssetResolutionFlags.IncludePreprocessed) == AssetResolutionFlags.IncludePreprocessed;

            var assetPath = Path.GetDirectoryName(Path.Combine(root, asset));
            if (!fileSystemService.DirectoryExists(assetPath))
            {
                return null;
            }

            var assetNoExtension = Path.GetFileNameWithoutExtension(asset);
            var assetMatches =
                from file in fileSystemService.ListFiles(assetPath, assetNoExtension + ".*")
                let fileExtension = Path.GetExtension(file)
                where
                    includePreprocessed || !fileExtension.Equals(PreprocessedFileExtension, StringComparison.InvariantCultureIgnoreCase)
                select file;

            var filteredExtension = extension;
            var filteredMatches =
                from assetMatch in assetMatches
                let assetExtension = Path.GetExtension(assetMatch)
                where
                    filteredExtension == null || assetExtension.Equals(filteredExtension, StringComparison.InvariantCultureIgnoreCase)
                select assetMatch;

            if (filteredMatches.Count() > 1)
                throw new FileNotFoundException(UltravioletStrings.FileAmbiguous.Format(asset));

            var singleMatch = filteredMatches.SingleOrDefault();
            if (singleMatch != null)
            {
                extension = Path.GetExtension(singleMatch);
                return singleMatch;
            }
            return null;
        }

        /// <summary>
        /// Gets the path to the specified asset.
        /// </summary>
        private String GetAssetPath(String asset, String extension, out String directory, out Boolean overridden, AssetResolutionFlags flags = AssetResolutionFlags.Default)
        {
            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return GetAssetPath(asset, extension, primaryDisplayDensity, out directory, out overridden, flags);
        }

        /// <summary>
        /// Gets the path to the specified asset.
        /// </summary>
        private String GetAssetPath(String asset, String extension, ScreenDensityBucket density, 
            out String directory, out Boolean overridden, AssetResolutionFlags flags = AssetResolutionFlags.Default)
        {
            var specifiedExtension = Path.GetExtension(asset);
            if (extension == null && !String.IsNullOrWhiteSpace(specifiedExtension))
                extension = specifiedExtension;

            var isLoadedFromSln = (flags & AssetResolutionFlags.LoadFromSolutionDirectory) == AssetResolutionFlags.LoadFromSolutionDirectory;
            var rootdir = isLoadedFromSln ? FindSolutionDirectory() ?? RootDirectory : RootDirectory;
            var path = GetAssetPathFromDirectory(rootdir, asset, ref extension, flags);
            directory = rootdir;
            overridden = false;

            foreach (var dir in OverrideDirectories)
            {
                var dirPath = GetAssetPathFromDirectory(dir, asset, ref extension, flags);
                if (dirPath != null)
                {
                    directory = dir;
                    path = dirPath;
                    overridden = true;
                }
            }

            var performSubstitution = (flags & AssetResolutionFlags.PerformSubstitution) == AssetResolutionFlags.PerformSubstitution;
            if (performSubstitution && path != null && !Path.HasExtension(asset))
            {
                var substitution = ListPossibleSubstitutions(directory, path, ScreenDensityBucket.Desktop, density)
                    .Take(1).SingleOrDefault();

                if (substitution != null)
                {
                    flags &= ~AssetResolutionFlags.PerformSubstitution;
                    path = GetAssetPathFromDirectory(directory, substitution, ref extension, flags);
                }
            }

            if (isLoadedFromSln && path == null)
            {
                flags &= ~AssetResolutionFlags.LoadFromSolutionDirectory;
                path = GetAssetPath(asset, extension, out directory, out overridden, flags);
            }

            return path;
        }
        
        /// <summary>
        /// Gets the metadata for the specified asset.
        /// </summary>
        private AssetMetadata GetAssetMetadata(String asset, Boolean includePreprocessedFiles, Boolean includeDetailedMetadata, Boolean fromsln)
        {
            var primaryDisplay = Ultraviolet.GetPlatform().Displays.PrimaryDisplay;
            var primaryDisplayDensity = primaryDisplay?.DensityBucket ?? ScreenDensityBucket.Desktop;

            return GetAssetMetadata(asset, primaryDisplayDensity, includePreprocessedFiles, includeDetailedMetadata, fromsln);
        }

        /// <summary>
        /// Gets the metadata for the specified asset.
        /// </summary>
        private AssetMetadata GetAssetMetadata(String asset, ScreenDensityBucket density, Boolean includePreprocessedFiles, Boolean includeDetailedMetadata, Boolean fromsln)
        {
            // If we're given a full filename with extension, return that.
            var assetDirectory = String.Empty;
            var assetOverridden = false;
            var assetExtension = Path.GetExtension(asset);
            if (!String.IsNullOrEmpty(assetExtension))
            {
                var assetPath = GetAssetPath(asset, assetExtension, density, out assetDirectory, out assetOverridden, AssetResolutionFlags.Default |
                    (fromsln ? AssetResolutionFlags.LoadFromSolutionDirectory : AssetResolutionFlags.None));
                if (assetPath != null)
                {
                    if (includePreprocessedFiles || !IsPreprocessedFile(asset))
                        return CreateMetadataFromFile(asset, assetPath, assetDirectory, assetOverridden, includeDetailedMetadata, fromsln, density);
                }
                throw new FileNotFoundException(UltravioletStrings.FileNotFound.Format(asset));
            }

            // Find the highest-ranking preprocessed file, if one exists.
            if (includePreprocessedFiles)
            {
                var assetPathPreprocessed = GetAssetPath(asset, PreprocessedFileExtension, density, out assetDirectory, out assetOverridden, AssetResolutionFlags.Default |
                    (fromsln ? AssetResolutionFlags.LoadFromSolutionDirectory : AssetResolutionFlags.None));
                if (assetPathPreprocessed != null)
                {
                    return CreateMetadataFromFile(asset, assetPathPreprocessed, assetDirectory, assetOverridden, includeDetailedMetadata, fromsln, density);
                }
            }

            // Find the highest-ranking metadata file, if one exists.
            var assetPathMetadata = GetAssetPath(asset, MetadataFileExtensionXml, density, out assetDirectory, out assetOverridden, AssetResolutionFlags.PerformSubstitution |
                (fromsln ? AssetResolutionFlags.LoadFromSolutionDirectory : AssetResolutionFlags.None));
            if (assetPathMetadata != null)
            {
                return CreateMetadataFromFile(asset, assetPathMetadata, assetDirectory, assetOverridden, includeDetailedMetadata, fromsln, density);
            }

            // Find the highest-ranking raw file.
            var assetPathRaw = GetAssetPath(asset, null, density, out assetDirectory, out assetOverridden, AssetResolutionFlags.PerformSubstitution | 
                (fromsln ? AssetResolutionFlags.LoadFromSolutionDirectory : AssetResolutionFlags.None));
            if (assetPathRaw != null)
            {
                return CreateMetadataFromFile(asset, assetPathRaw, assetDirectory, assetOverridden, includeDetailedMetadata, fromsln, density);
            }

            // If we still have no matches, we can't find the file.
            throw new FileNotFoundException(UltravioletStrings.FileNotFound.Format(asset));
        }

        /// <summary>
        /// Creates an asset metadata object from the specified asset file.
        /// </summary>
        private AssetMetadata CreateMetadataFromFile(String asset, String filename, String rootdir, Boolean overridden, Boolean includeDetailedMetadata, Boolean fromsln, ScreenDensityBucket density)
        {
            var source = (Object)asset;
            return CreateMetadataFromSource(ref source, filename, rootdir, overridden, includeDetailedMetadata, fromsln, density);
        }

        /// <summary>
        /// Creates an asset metadata object from the specified stream.
        /// </summary>
        private AssetMetadata CreateMetadataFromStream(ref Stream stream, String filename, String rootdir, Boolean overridden, Boolean includeDetailedMetadata, Boolean fromsln, ScreenDensityBucket density)
        {
            var source = (Object)stream;
            var result = CreateMetadataFromSource(ref source, filename, rootdir, overridden, includeDetailedMetadata, fromsln, density);
            stream = (Stream)source;
            return result;
        }

        /// <summary>
        /// Creates an asset metadata object from the specified source object.
        /// </summary>
        private AssetMetadata CreateMetadataFromSource(ref Object source, String filename, String rootdir, Boolean overridden, Boolean includeDetailedMetadata, Boolean fromsln, ScreenDensityBucket density)
        {
            var isStream = (source is Stream);

            if (IsMetadataFile(filename, out var extension) && includeDetailedMetadata)
            {
                var isJson = (extension == MetadataFileExtensionJson);

                var wrappedFilename = default(String);
                var wrappedAssetPath = default(String);

                var importerMetadata = default(Object);
                var processorMetadata = default(Object);

                if (isJson)
                {
                    var json = default(JObject);
                    if (isStream)
                    {
                        using (var sreader = new StreamReader((Stream)source, Encoding.UTF8, false, 1024, true))
                        using (var jreader = new JsonTextReader(sreader))
                        {
                            json = (JObject)JToken.ReadFrom(jreader);
                        }
                    }
                    else
                    {
                        using (var stream = fileSystemService.OpenRead(filename))
                        using (var sreader = new StreamReader(stream))
                        using (var jreader = new JsonTextReader(sreader))
                        {
                            json = (JObject)JToken.ReadFrom(jreader);
                        }
                    }

                    wrappedFilename = json["asset"].Value<String>();
                    importerMetadata = (JObject)json["importerMetadata"];
                    processorMetadata = (JObject)json["processorMetadata"];
                }
                else
                {
                    var xml = default(XDocument);
                    if (isStream)
                    {
                        xml = XDocument.Load((Stream)source);
                    }
                    else
                    {
                        using (var stream = fileSystemService.OpenRead(filename))
                            xml = XDocument.Load(stream);
                    }

                    wrappedFilename = xml.Root.ElementValueString("Asset");
                    importerMetadata = xml.Root.Element("ImporterMetadata");
                    processorMetadata = xml.Root.Element("ProcessorMetadata");
                }

                if (String.IsNullOrWhiteSpace(wrappedFilename) || String.IsNullOrWhiteSpace(Path.GetExtension(wrappedFilename)))
                    throw new InvalidDataException(UltravioletStrings.AssetMetadataHasInvalidFilename);

                var directory = Path.GetDirectoryName(filename);
                var relative = isStream ? wrappedFilename : fileSystemService.GetRelativePath(RootDirectory, Path.Combine(directory, wrappedFilename));

                var wrappedAssetDirectory = String.Empty;
                var wrappedAssetOverridden = false;
                wrappedAssetPath = GetAssetPath(relative, Path.GetExtension(relative), out wrappedAssetDirectory, out wrappedAssetOverridden);

                if (String.IsNullOrEmpty(wrappedAssetPath) || !fileSystemService.FileExists(wrappedAssetPath))
                    throw new InvalidDataException(UltravioletStrings.AssetMetadataFileNotFound);

                source = null;

                return new AssetMetadata(wrappedAssetOverridden ? wrappedAssetDirectory : null,
                    source as String, wrappedAssetPath, importerMetadata, processorMetadata, true, false, isJson, fromsln, density);
            }
            return new AssetMetadata(overridden ? rootdir : null, source as String, filename, null, null, true, false, false, fromsln, density);
        }

        /// <summary>
        /// Gets a list of assets in the specified asset directory.
        /// </summary>
        /// <param name="path">The directory to evaluate.</param>
        /// <param name="searchPattern">The search string to match against the names of files in the path.</param>
        /// <returns>The list of assets in the specified asset directory.</returns>
        private Dictionary<String, String> GetAssetsInDirectoryInternal(String path, String searchPattern)
        {
            var results = new Dictionary<String, String>();

            GetAssetsInDirectory(RootDirectory, path, searchPattern, results);

            if (!Path.IsPathRooted(path))
            {
                foreach (var dir in overrideDirectories)
                {
                    GetAssetsInDirectory(dir, path, searchPattern, results);
                }
            }

            return results;
        }

        /// <summary>
        /// Updates the specified result set with the list of assets at the specified path.
        /// </summary>
        /// <param name="directory">The root directory being examined.</param>
        /// <param name="path">The path relative to the root directory being examined.</param>
        /// <param name="searchPattern">The search string to match against the names of files in the path.</param>
        /// <param name="results">The result set to update.</param>
        private void GetAssetsInDirectory(String directory, String path, String searchPattern, Dictionary<String, String> results)
        {
            var root = String.IsNullOrEmpty(directory) ? fileSystemService.GetCurrentDirectory() : fileSystemService.GetFullPath(directory);

            var assetRoot = fileSystemService.GetRelativePath(fileSystemService.GetCurrentDirectory(), root);
            var assetDirectory = Path.Combine(assetRoot, path ?? String.Empty);
            if (!fileSystemService.DirectoryExists(assetDirectory))
                return;

            var assets =
                from f in fileSystemService.ListFiles(assetDirectory, searchPattern)
                let relative = fileSystemService.GetRelativePath(root, f)
                let absolute = fileSystemService.GetFullPath(Path.Combine(root, relative))
                select new { RelativePath = relative, AbsolutePath = absolute };

            foreach (var asset in assets)
            {
                results[asset.RelativePath] = asset.AbsolutePath;
            }
        }

        /// <summary>
        /// Updates the specified result set with the list of directories at the specified path.
        /// </summary>
        /// <param name="directory">The root directory being examined.</param>
        /// <param name="path">The path relative to the root directory being examined.</param>
        /// <param name="searchPattern">The search string to match against the names of directories in the path.</param>
        /// <param name="results">The result set to update.</param>
        private void GetSubdirectories(String directory, String path, String searchPattern, Dictionary<String, String> results)
        {
            var root = String.IsNullOrEmpty(directory) ? fileSystemService.GetCurrentDirectory() : fileSystemService.GetFullPath(directory);

            var assetRoot = fileSystemService.GetRelativePath(fileSystemService.GetCurrentDirectory(), root);
            var assetDirectory = Path.Combine(assetRoot, path ?? String.Empty);
            if (!fileSystemService.DirectoryExists(assetDirectory))
                return;

            var assets =
                from d in fileSystemService.ListDirectories(assetDirectory, searchPattern)
                let relative = fileSystemService.GetRelativePath(root, d)
                let absolute = fileSystemService.GetFullPath(Path.Combine(root, relative))
                select new { RelativePath = relative, AbsolutePath = absolute };

            foreach (var asset in assets)
            {
                results[asset.RelativePath] = asset.AbsolutePath;
            }
        }

        /// <summary>
        /// Updates the content manager's internal cache with the specified object instance.
        /// </summary>
        private void UpdateCache(String asset, AssetMetadata metadata, ref Object instance, Type type, ScreenDensityBucket densityBucket)
        {
            lock (cacheSyncObject)
            {
                var assetDensityBucket = densityBucket;
                var assetOrigin = metadata.IsOverridden ? metadata.OverrideDirectory : null;

                if (!assetCache.TryGetValue(asset, out var assetCacheEntry))
                {
                    assetCacheEntry = new AssetCacheEntry(assetDensityBucket, assetOrigin, instance, type);
                    assetCache[asset] = assetCacheEntry;

                    if (!assetFlags.ContainsKey(asset))
                        assetFlags[asset] = AssetFlags.None;
                }
                else
                {
                    assetCacheEntry.SetVersion(assetDensityBucket, assetOrigin, instance);
                }
            }
        }

        /// <summary>
        /// Purges any asset versions for screen densities which are not in use.
        /// </summary>
        private void PurgeUnusedScreenDensities()
        {
            var usedScreenDensities = Ultraviolet.GetPlatform().Displays.Select(x => x.DensityBucket).Distinct().ToArray();
            var purgedCacheEntries = new List<String>();

            foreach (var kvp in assetCache)
            {
                if (kvp.Value.PurgeUnusedVersions(usedScreenDensities))
                    purgedCacheEntries.Add(kvp.Key);
            }

            foreach (var purgedCacheEntry in purgedCacheEntries)
                PurgeCache(purgedCacheEntry, false);
        }

        /// <summary>
        /// Gets a value indicating whether dependency tracking is suppressed for this content manager.
        /// </summary>
        private Boolean IsDependencyTrackingSuppressed
        {
            get
            {
                return (Ultraviolet.Platform == UltravioletPlatform.Android || Ultraviolet.Platform == UltravioletPlatform.iOS) ||
                    GloballySuppressDependencyTracking || SuppressDependencyTracking;
            }
        }

        // State values.
        private readonly ContentOverrideDirectoryCollection overrideDirectories;
        private readonly Dictionary<String, AssetCacheEntry> assetCache = new Dictionary<String, AssetCacheEntry>();
        private readonly Dictionary<String, AssetFlags> assetFlags = new Dictionary<String, AssetFlags>();
        private readonly FileSystemService fileSystemService;
        private readonly Object cacheSyncObject = new Object();

        // File watching.
        private FileSystemWatcher rootFileSystemWatcher;
        private Dictionary<String, IAssetWatcherCollection> assetWatchers;
        private Dictionary<String, IAssetDependencyCollection> assetDependencies;
        private Dictionary<String, Dictionary<Byte, Object>> sharedWatchedAssets;
        private String solutionDirectory;

        // The file extensions associated with preprocessed binary data and asset metadata files.
        private const String PreprocessedFileExtension = ".uvc";
        private const String MetadataFileExtensionXml = ".uvmeta";
        private const String MetadataFileExtensionJson = ".jsmeta";

        // Files waiting to be deleted as part of a batch.
        private readonly List<String> filesPendingDeletion = new List<String>();
        private Boolean batchDeletedFiles;
        private Boolean batchDeletedFilesGuarantee;

        // The supported screen density buckets.
        private static readonly IEnumerable<ScreenDensityBucket> ScreenDensityBuckets;
    }
}
