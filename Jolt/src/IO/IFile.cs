using System.IO;

namespace Jolt.IO
{
    /// <summary>
    /// An abstraction of the functionality offered by the <c>System.IO.File</c> class.
    /// </summary>
    public interface IFile
    {
        /// <summary>
        /// Determines whether the specified file exists. See <see cref="File.Exists"/> for details.
        /// </summary>
        /// <returns>
        /// true if the caller has the required permissions and <paramref name="path"/> contains the name of an existing file; otherwise, false. This method also returns false if <paramref name="path"/> is null, an invalid path, or a zero-length string. If the caller does not have sufficient permissions to read the specified file, no exception is thrown and the method returns false regardless of the existence of <paramref name="path"/>.
        /// </returns>
        /// <param name="path">The file to check.</param>
        bool Exists(string path);

        /// <summary>
        /// Opens an existing UTF-8 encoded text file for reading. See <see cref="File.OpenText"/> for details.
        /// </summary>
        /// <returns>
        /// A <see cref="StreamReader"/> on the specified path.
        /// </returns>
        StreamReader OpenText(string path);
    }
}