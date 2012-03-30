
namespace VsVim
{
    /// <summary>
    /// Interface for getting information about the R# install.  
    /// 
    /// TODO: Ideally this should exist at all.  It would be great if R# could be 
    /// done purely as a silenty MEF plugin 
    /// </summary>
    internal interface IResharperUtil
    {
        bool IsInstalled { get; }
    }
}
