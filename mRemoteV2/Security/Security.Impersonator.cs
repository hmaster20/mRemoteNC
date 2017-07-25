using Microsoft.VisualBasic;
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.Permissions;
using mRemoteNC.App;


//[assembly: SecurityPermissionAttribute(SecurityAction.RequestMinimum, UnmanagedCode = true), PermissionSetAttribute(SecurityAction.RequestMinimum, Name = "FullTrust")]

namespace mRemoteNC
{
    namespace Security
    {
        public class Impersonator
        {
            #region "Logon API"

            private const int LOGON32_PROVIDER_DEFAULT = 0;
            public const int LOGON32_LOGON_INTERACTIVE = 2;
            private const int LOGON32_LOGON_NEW_CREDENTIALS = 9;

            private const int SecurityImpersonation = 2;

            [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern int LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
                                                int dwLogonType, int dwLogonProvider, ref IntPtr phToken);

            private const int FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x100;
            private const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x200;

            private const int FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;

            [DllImport("kernel32.dll")]
            private static extern int FormatMessage(int dwFlags, ref IntPtr lpSource, int dwMessageId, int dwLanguageId,
                                                    ref String lpBuffer, int nSize, ref IntPtr Arguments);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]

            private static extern bool CloseHandle(IntPtr handle);

            [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]

            private static extern int DuplicateToken(IntPtr ExistingTokenHandle, int SECURITY_IMPERSONATION_LEVEL,
                                                     ref IntPtr DuplicateTokenHandle);

            #endregion

            private IntPtr tokenHandle = new IntPtr(0);

            private WindowsImpersonationContext impersonatedUser = null;
            // GetErrorMessage formats and returns an error message corresponding to the input errorCode.
            private string GetErrorMessage(int errorCode)
            {
                int messageSize = 255;
                string lpMsgBuf = "";
                int dwFlags = FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM |
                              FORMAT_MESSAGE_IGNORE_INSERTS;

                IntPtr ptrlpSource = IntPtr.Zero;
                IntPtr prtArguments = IntPtr.Zero;

                int retVal = FormatMessage(dwFlags, ref ptrlpSource, errorCode, 0, ref lpMsgBuf, messageSize,
                                           ref prtArguments);
                return lpMsgBuf.Trim(new char[]
                    {
                        Convert.ToChar(Constants.vbCr),
                        Convert.ToChar(Constants.vbLf)
                    });
            }

            [PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
            public void StartImpersonation(string DomainName, string UserName, string Password)
            {
                try
                {
                    if ((impersonatedUser != null))
                        throw new Exception("Already impersonating a user.");

                    tokenHandle = IntPtr.Zero;

                    int returnValue = LogonUser(UserName, DomainName, Password, LOGON32_LOGON_NEW_CREDENTIALS,
                                                LOGON32_PROVIDER_DEFAULT, ref tokenHandle);

                    if (0 == returnValue)
                    {
                        int errCode = Marshal.GetLastWin32Error();
                        string errMsg = "LogonUser failed with error code: " + errCode.ToString() + "(" +
                                        GetErrorMessage(errCode) + ")";
                        ApplicationException exLogon = new ApplicationException(errMsg);
                        throw exLogon;
                    }

                    WindowsIdentity newId = new WindowsIdentity(tokenHandle);
                    impersonatedUser = newId.Impersonate();
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddMessage(Messages.MessageClass.WarningMsg,
                                                        "Starting Impersonation failed (Sessions feature will not work)" +
                                                        Constants.vbNewLine + ex.Message, true);
                }
            }

            [PermissionSetAttribute(SecurityAction.Demand, Name = "FullTrust")]
            public void StopImpersonation()
            {
                if (impersonatedUser == null)
                    return;

                try
                {
                    impersonatedUser.Undo();
                    // Stop impersonating the user.
                }
                catch (Exception ex)
                {
                    Runtime.MessageCollector.AddMessage(Messages.MessageClass.WarningMsg,
                                                        "Stopping Impersonation failed" + Constants.vbNewLine +
                                                        ex.Message, true);
                    throw;

                }
                finally
                {
                    if (tokenHandle!=IntPtr.Zero)
                        CloseHandle(tokenHandle);

                    impersonatedUser = null;
                }
            }
        }
    }
}