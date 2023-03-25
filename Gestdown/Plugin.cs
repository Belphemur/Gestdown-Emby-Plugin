using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Drawing;

namespace Gestdown
{
    /// <summary>
    /// Class Plugin
    /// </summary>
    public class Plugin : BasePlugin, IHasWebPages, IHasThumbImage
    {
        /// <summary>
        /// Gets the name of the plugin
        /// </summary>
        /// <value>The name.</value>
        public override string Name => "Addic7ed Proxy: Gestdown";

        /// <summary>
        /// Gets the description.
        /// </summary>
        /// <value>The description.</value>
        public override string Description => "Proxy service for the Addic7ed website. No Login required.";

        private readonly Guid _id = new("53B91E1D-888B-4698-AC30-A19CF27B3890");
        public override Guid Id => _id;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return Array.Empty<PluginPageInfo>();
        }

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.jpg");
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Jpg;
    }
}
