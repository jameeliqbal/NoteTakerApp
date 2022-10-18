using Dropbox.Api;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using WForms=System.Windows.Forms;

namespace DropboxAudioTranscription.Modules
{

    public class DropboxModule
    {
        private bool cancelAuthentication;
        private bool hasTimedOut;
        private HttpListener http = null;
        private Process authProcess = null;
        private Timer processTimer = null;
        private IntPtr DropboxLoginPageWindowHandle;

        // URL to receive OAuth 2 redirect from Dropbox server.
        // You also need to register this redirect URL on https://www.dropbox.com/developers/apps.
        public readonly Uri DropboxRedirectUri;

        // This loopback host is for demo purpose. If this port is not
        // available on your machine you need to update this URL with an unused port.
        public const string DropboxLoopbackHost = "http://127.0.0.1:52475/";

        // URL to receive access token from JS.
        private readonly Uri DropboxJSRedirectUri;

        public event EventHandler Authenticated;
        public event EventHandler<DropboxAuthenticationCancelledEventArgs> AuthenticationCancelled;
        public event EventHandler AuthenticationTimedout;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        //+++++++++
        private readonly DropboxAccount account;
        

        public DropboxModule(DropboxAccount account)
        {
            this.account = account;
            DropboxRedirectUri = new Uri(DropboxLoopbackHost + "authorize");
            DropboxJSRedirectUri = new Uri(DropboxLoopbackHost + "token");
        }

        #region Account Services

        public async Task Login()
        {
           
            try
            {
                cancelAuthentication = false;

                var IsTokenAcquired = await AcquireAccessToken(null, IncludeGrantedScopes.None, cancelAuthentication);

                //if (hasTimedOut) return;
                if (hasTimedOut || cancelAuthentication)
                {
                    //AbortSignin();
                    return;
                }

                if (!IsTokenAcquired) return;
                await GetAccountInfo();

                account.IsConnected = true;
                Authenticated?.Invoke(this, EventArgs.Empty);
            }
            catch
            {

                throw;
            }
        }

        private void AbortSignin()
        {
            StopTimer();
            StopHTTPListener(true);

            hasTimedOut = false;
            cancelAuthentication = false;
        }

        public void CancelSignIn()
        {
            cancelAuthentication = true;
            AbortSignin();
            CloseBrowser();
            var e = new DropboxAuthenticationCancelledEventArgs("Authorization cancelled by user on UI");
            OnSignInCancelledEvent(e);

        }

        private void OnSignInCancelledEvent(DropboxAuthenticationCancelledEventArgs e)
        {
            EventHandler<DropboxAuthenticationCancelledEventArgs> raiseAuthenticationCancelledEvent = AuthenticationCancelled;

            if (raiseAuthenticationCancelledEvent != null)
            {
                raiseAuthenticationCancelledEvent(this, e);
            }
        }

        public async Task SignOut()
        {
            var client = GetDropboxClient();
            await client.Auth.TokenRevokeAsync();
        }
        #endregion // Account Services

        #region Private Methods - AccountServices

        /// <summary>
        /// Acquires a dropbox access token and saves it to the default settings for the app.
        /// <para>
        /// This fetches the access token from the applications settings, if it is not found there
        /// (or if the user chooses to reset the settings) then the UI in <see cref="LoginForm"/> is
        /// displayed to authorize the user.
        /// </para>
        /// </summary>
        /// <returns>A valid uid if a token was acquired or null.</returns>
        private async Task<bool> AcquireAccessToken(string[] scopeList, IncludeGrantedScopes includeGrantedScopes, bool cancelAuthentication)
        {


            try
            {
                //login to dropbox
                Console.WriteLine("Waiting for credentials.");
                var tokenResult = await LoginToDropbox(scopeList, includeGrantedScopes);

                if (hasTimedOut || cancelAuthentication)
                {
                    
                    return false;
                }


                if (tokenResult == null) return false;

                account.AccessToken = tokenResult.AccessToken;
                account.RefreshToken = tokenResult.RefreshToken;
                account.UID = tokenResult.Uid;
                account.TokenExpiresAt = (DateTime)tokenResult.ExpiresAt;

                Console.WriteLine("Uid: {0}", account.UID);
                Console.WriteLine("AccessToken: {0}", account.AccessToken);
                if (tokenResult.RefreshToken != null)
                {
                    Console.WriteLine("RefreshToken: {0}", account.RefreshToken);
                }

                if (tokenResult.ExpiresAt != null)
                {
                    Console.WriteLine("ExpiresAt: {0}", tokenResult.ExpiresAt);
                }

                if (tokenResult.ScopeList != null)
                {
                    Console.WriteLine("Scopes: {0}", String.Join(" ", tokenResult.ScopeList));
                }

                return true;
            }
            catch  (OperationCanceledException ocx)
            {
                cancelAuthentication = true;
                AbortSignin();
                CloseBrowser();

                var dpcancellation = new DropboxAuthenticationCancelledEventArgs(ocx.Message);
                OnSignInCancelledEvent(dpcancellation);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error: {0}", e.Message);
                throw;
            }

            return false;
        }

        private async Task<OAuth2Response> LoginToDropbox(string[] scopeList, IncludeGrantedScopes includeGrantedScopes)
        {
            try
            {
                http = null;

                var state = Guid.NewGuid().ToString("N");
                var OAuthFlow = new PKCEOAuthFlow();
                var authorizeUri = OAuthFlow.GetAuthorizeUri(OAuthResponseType.Code,
                                        account.APIKey,
                                        DropboxRedirectUri.ToString(),
                                        state: state,
                                        tokenAccessType: TokenAccessType.Offline,
                                        scopeList: scopeList,
                                        includeGrantedScopes: includeGrantedScopes);

                //login to dropbox in the browser
                authProcess = LaunchBrowser(authorizeUri);
                authProcess.Refresh();
                DropboxLoginPageWindowHandle = authProcess.Handle;
                //Debug.WriteLine("pROcess started: " + authProcess.StartInfo.FileName);

                //Start local server to listen to reponse from dropbox page on browser
                StartHTTPListener();
                //Debug.WriteLine("http listener started ");

                // countdown for auto cancellation
                StartTimer(45000);

                if (cancelAuthentication)
                {
                    return null;
                }

                // Handle OAuth redirect and send URL fragment to local server using JS.
                //Debug.WriteLine("HandleOAuth2Redirect   calling");
                await HandleOAuth2Redirect(http);

                if (hasTimedOut || cancelAuthentication)
                {
                    return null;
                }
                //Debug.WriteLine("HandleOAuth2Redirect    complete");

                //// Handle redirect from JS and process OAuth response.
                //Debug.WriteLine("redirect from JS   calling");
                var redirectUri = await HandleJSRedirect(http);
                if (redirectUri == null) return null;
                var result = redirectUri.Query.Split('&');
                if (result[0].Contains("error"))
                {
                    var errorMessage =  result[0].Split('=')[1];
                    errorMessage += " - "+ result[1].Split('=')[1].Replace("+", " ");
                     
                    throw new OperationCanceledException(errorMessage);
                }

                if (hasTimedOut || cancelAuthentication)
                {
                    return null;
                }

                //Debug.WriteLine("Exchanging code for token");
                //Debug.WriteLine("redirect from JS   complete");
                var tokenResult = await OAuthFlow.ProcessCodeFlowAsync(redirectUri, account.APIKey, DropboxRedirectUri.ToString(), state);
                //Debug.WriteLine("Finished Exchanging Code for Token");

                //end this session
                StopHTTPListener();

                //if (hasTimedOut) return;

                if (hasTimedOut || cancelAuthentication)
                {
                    return null;
                }

                //start new session to Notifiy Token Received message in the browser to  the user 
                StartHTTPListener();
                await HandleTokenReceivedRedirect(http);
                StopTimer();
                StopHTTPListener();
                CloseBrowser();

                return tokenResult;
            }
            catch (OperationCanceledException ex)
            {
                StopTimer();

                StopHTTPListener();


                throw;
            }

        }

        private void StopTimer()
        {
            if (processTimer == null) return;
            if (hasTimedOut) return;

            hasTimedOut = false;

            processTimer.Stop();
            processTimer.Enabled = false;
            processTimer.Dispose();
            processTimer = null;
        }

        private void StopHTTPListener(bool abort=false)
        {
            if (http != null)
            {
                try
                {
                    if (abort)
                        http.Abort();
                    else
                    {
                        if (http.IsListening)
                            http.Stop();
                        http.Close();
                    }
                        
                    http.Prefixes?.Remove(DropboxLoopbackHost);
                    

                }
                catch (ObjectDisposedException)
                {

                    Debug.WriteLine("http has already aborted!");
                }
                http = null;
            }
        }

        private void StartHTTPListener()
        {
            http = new HttpListener();
            http.Prefixes.Add(DropboxLoopbackHost);
            http.Start();
        }

        private void StartTimer(double intervalDuration)
        {
            processTimer = new System.Timers.Timer(intervalDuration);
            processTimer.AutoReset = false;
            processTimer.Elapsed += ProcessTimer_Elapsed;
            processTimer.Enabled = true;
            processTimer.Start();
            Debug.WriteLine("Timer started ");
        }

        private Process LaunchBrowser(Uri authorizeUri)
        {
            //detect browser
            string browserPath = DetectDefaultBrowser();

            var chromiumCLISwitches = " --app-auto-launched --new-window";
            var processArgs = authorizeUri.ToString() + chromiumCLISwitches;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Normal,
                FileName = browserPath,
                Arguments = processArgs,
                UseShellExecute = true
            };

            return Process.Start(psi);
        }

        private string DetectDefaultBrowser()
        {
            string browserPath = string.Empty;

            const string defaultBrowserKeyPath = @"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";
            using (var defaultBrowserKey = Registry.CurrentUser.OpenSubKey(defaultBrowserKeyPath))
            {
                var defaultBrowserId = defaultBrowserKey.GetValue("ProgId");
                var defaultBrowserExecutablePath = $@"{defaultBrowserId}\shell\open\command";
                using (var defaultBrowserExecutableKey = Registry.ClassesRoot.OpenSubKey(defaultBrowserExecutablePath))
                {
                    var defaultValueName = string.Empty; //(Default) registry item
                    var defaultBrowserExecutableKeyValue = defaultBrowserExecutableKey.GetValue(defaultValueName);
                    var seperator = '\"';
                    var doubleQuotes = "\"";
                    browserPath = defaultBrowserExecutableKeyValue.ToString().Split(seperator)[1].Replace(doubleQuotes, string.Empty).Trim();
                }
            }
            return browserPath;

            //var ir = IsProcessOpen(Path.GetFileNameWithoutExtension(browserPath));
        }

        public bool IsProcessOpen(string name)
        {
            //here we're going to get a list of all running processes on
            //the computer
            var processes = Process.GetProcesses();
            foreach (Process clsProcess in processes)
            {
                //now we're going to see if any of the running processes
                //match the currently running processes. Be sure to not
                //add the .exe to the name you provide, i.e: NOTEPAD,
                //not NOTEPAD.EXE or false is always returned even if
                //notepad is running.
                //Remember, if you have the process running more than once, 
                //say IE open 4 times the loop thr way it is now will close all 4,
                //if you want it to just close the first one it finds
                //then add a return; after the Kill
                if (clsProcess.ProcessName.Contains(name))
                {
                    //if the process is found to be running then we
                    //return a true
                    return true;
                }
            }
            //otherwise we return a false
            return false;
        }


        private void ProcessTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (authProcess == null || http == null) return;

            hasTimedOut = true;

            StopHTTPListener(true);
            CloseAuthProcess();
            CloseBrowser();

            //notifiy ui of timeout
            AuthenticationTimedout?.Invoke(this, EventArgs.Empty);

            hasTimedOut = false;

        }

        private void CloseAuthProcess()
        {
            try
            {
                //authProcess.CloseMainWindow();
                
                authProcess.Close();
                authProcess.Dispose();
                authProcess = null;

            }
            catch (Exception ex)
            {

                throw;
            }
        }

        private void AuthProcess_Exited(object sender, EventArgs e)
        {
            if (authProcess == null || hasTimedOut) return;
            Debug.WriteLine("EXIT CODE: " + authProcess.ExitCode);
            Debug.WriteLine("RESPONDING: " + authProcess.Responding);
            Debug.WriteLine("HASEXITED: " + authProcess.HasExited);
            if (!authProcess.CloseMainWindow())
            {
                authProcess.Kill();
            }
            ////authProcess.Kill();
            //authProcess.Refresh();
            //if (http != null)
            //{
            //    if (http.IsListening)
            //        http.Stop();
            //}
            //    AuthenticationCancelled?.Invoke(this, EventArgs.Empty);
            //authProcess = null;
        }

        /// <summary>
        /// Handles the redirect from Dropbox server. Because we are using token flow, the local
        /// http server cannot directly receive the URL fragment. We need to return a HTML page with
        /// inline JS which can send URL fragment to local server as URL parameter.
        /// </summary>
        /// <param name="http">The http listener.</param>
        /// <returns>The <see cref="Task"/></returns>
        private async Task HandleOAuth2Redirect(HttpListener http)
        {
            try
            {

                //Debug.WriteLine("getting http context-strted");
                var context = await http.GetContextAsync();
                //Debug.WriteLine("getting http context -complete");

                // We only care about request to RedirectUri endpoint.
                while (context.Request.Url.AbsolutePath != DropboxRedirectUri.AbsolutePath)
                {
                    //Debug.WriteLine("getting http context-strted");
                    //Debug.WriteLine(context.Request.Url.AbsolutePath);

                    context = await http.GetContextAsync();
                }

                // Respond with a page which runs JS and sends URL fragment as query string
                // to TokenRedirectUri.
                context.Response.ContentType = "text/html";
                using (var file = File.OpenRead("dropbox\\index.html"))
                {
                    file.CopyTo(context.Response.OutputStream);
                }
                context.Response.OutputStream.Close();
            }
            catch (HttpListenerException)
            {
                Debug.WriteLine("HttpListenerException- Cancelled or timedout");

            }
            catch (ObjectDisposedException)
            {

                Debug.WriteLine("ObjectDisposedException -Cancelled or timedout");
            }

        }

        private async Task HandleTokenReceivedRedirect(HttpListener http)
        {
            var context = await http.GetContextAsync();

            // We only care about request to RedirectUri endpoint.
            while (context.Request.Url.AbsolutePath != DropboxJSRedirectUri.AbsolutePath)
            {
                context = await http.GetContextAsync();
            }

            context.Response.ContentType = "text/html";

            // Respond with a page which runs JS and sends URL fragment as query string
            // to TokenRedirectUri.
            using (var file = File.OpenRead("dropbox\\token.html"))
            {
                file.CopyTo(context.Response.OutputStream);
            }

            context.Response.OutputStream.Close();
        }

        /// <summary>
        /// Handle the redirect from JS and process raw redirect URI with fragment to
        /// complete the authorization flow.
        /// </summary>
        /// <param name="http">The http listener.</param>
        /// <returns>The <see cref="OAuth2Response"/></returns>
        private async Task<Uri> HandleJSRedirect(HttpListener http)
        {
            try
            {
                var context = await http.GetContextAsync();

                // We only care about request to TokenRedirectUri endpoint.
                while (context.Request.Url.AbsolutePath != DropboxJSRedirectUri.AbsolutePath)
                {
                    context = await http.GetContextAsync();
                }

                var redirectUri = new Uri(context.Request.QueryString["url_with_fragment"]);

                return redirectUri;

            }
            catch (NullReferenceException)
            {

                return null;
            }
        }


        private async Task GetAccountInfo()
        {
            // Specify socket level timeout which decides maximum waiting time when no bytes are
            // received by the socket.
            var client = GetDropboxClient();
            try
            {
                // This call should succeed since the correct scope has been acquired
                await GetCurrentAccount(client);

                Console.WriteLine("Oauth PKCE Test Complete!");
                Console.WriteLine("Exit with any key");
                //Console.ReadKey();
            }
            catch (HttpException e)
            {
                Console.WriteLine("Exception reported from RPC layer");
                Console.WriteLine("    Status code: {0}", e.StatusCode);
                Console.WriteLine("    Message    : {0}", e.Message);
                if (e.RequestUri != null)
                {
                    Console.WriteLine("    Request uri: {0}", e.RequestUri);
                }
            }
        }

        private DropboxClient GetDropboxClient()
        {

            try
            {
                var httpClient = new HttpClient(new HttpClientHandler())
                {
                    // Specify request level timeout which decides maximum time that can be spent on
                    // download/upload files.
                    Timeout = TimeSpan.FromMinutes(30)
                };

                var httpClientLongPoll = new HttpClient(new HttpClientHandler())
                {
                    // Specify request level timeout which decides maximum time that can be spent on
                    // long poll
                    Timeout = TimeSpan.FromSeconds(130)
                };
                try
                {
                    var config = new DropboxClientConfig("SimplePKCEOAuthApp")
                    {
                        LongPollHttpClient = httpClientLongPoll,
                        HttpClient = httpClient
                    };

                    return new DropboxClient(account.RefreshToken, account.APIKey, config);
                }
                catch (HttpException hex)
                {
                    Console.WriteLine("Exception reported from RPC layer");
                    Console.WriteLine($"    Status code: {hex.StatusCode}");
                    Console.WriteLine($"    Message    : {hex.Message}");
                    if (hex.RequestUri != null)
                    {
                        Console.WriteLine($"    Request uri: {hex.RequestUri}");
                    }
                    Console.WriteLine("Exit with any key");
                    throw;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("***ERRor***");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException?.Message);
                Console.WriteLine("Exit with any key");
                throw;
            }
        }

        /// <summary>
        /// Gets information about the currently authorized account.
        /// <para>
        /// This demonstrates calling a simple rpc style api from the Users namespace.
        /// </para>
        /// </summary>
        /// <param name="client">The Dropbox client.</param>
        /// <returns>An asynchronous task.</returns>
        private async Task GetCurrentAccount(DropboxClient client)
        {
            try
            {
                Console.WriteLine("Current Account:");
                var full = await client.Users.GetCurrentAccountAsync();
                account.Name = full.Name.DisplayName;
                account.Email = full.Email;

                Console.WriteLine("Account id    : {0}", full.AccountId);
                Console.WriteLine("Country       : {0}", full.Country);
                Console.WriteLine("Email         : {0}", full.Email);
                Console.WriteLine("Is paired     : {0}", full.IsPaired ? "Yes" : "No");
                Console.WriteLine("Locale        : {0}", full.Locale);
                Console.WriteLine("Name");
                Console.WriteLine("  Display  : {0}", full.Name.DisplayName);
                Console.WriteLine("  Familiar : {0}", full.Name.FamiliarName);
                Console.WriteLine("  Given    : {0}", full.Name.GivenName);
                Console.WriteLine("  Surname  : {0}", full.Name.Surname);
                Console.WriteLine("Referral link : {0}", full.ReferralLink);

                if (full.Team != null)
                {
                    Console.WriteLine("Team");
                    Console.WriteLine("  Id   : {0}", full.Team.Id);
                    Console.WriteLine("  Name : {0}", full.Team.Name);
                }
                else
                {
                    Console.WriteLine("Team - None");
                }
            }
            catch (Exception e)
            {
                throw;
            }

        }


        /// <summary>
        /// REF1: https://stackoverflow.com/questions/20041514/how-to-send-a-key-to-a-process
        /// REF2: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.sendkeys?redirectedfrom=MSDN&view=windowsdesktop-6.0
        /// </summary>
        private void CloseBrowser()
        {
           

            foreach (Process p in Process.GetProcesses())
            {
                //Debug.WriteLine("Main windo handle = " + p.ProcessName + " - " + p.MainWindowTitle + " - " + p.MainWindowHandle);
                if ((p.MainWindowTitle.Contains("API Request Authorization - Dropbox") || p.MainWindowTitle.Contains("127.0.0.1")) &&
                    p.MainWindowHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(p.MainWindowHandle);
                    WForms.SendKeys.SendWait("^{F4}");
                }
            }

        }

        #endregion //Private Methods - Account Services


    }

    public class DropboxAuthenticationCancelledEventArgs : EventArgs
    {
        public DropboxAuthenticationCancelledEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; set; }
    }
}
