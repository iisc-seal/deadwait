using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Net.Http.Formatting;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Reflection;
using Microsoft.Azure.WebJobs;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.WebJobs.Script.WebHost;

namespace Stubs
{
    public class AsyncStreamStub
    {
        public static Task<int> ReadAsync(Stream thisref, byte[] buffer, int offset, int count)
        {
            var x = new Task<int>(() => 10);
            return x;
        }

        public static Task<int> ReadAsync(Stream thisref, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var x = new Task<int>(() => 10);
            return x;
        }

        public static Task WriteAsync(Stream thisref, byte[] buffer, int offset, int count)
        {
            var x = new Task(() => { int i = 0; i++; });
            return x;
        }

        public static Task WriteAsync(Stream thisref, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var x = new Task(() => { int i = 0; i++; });
            return x;
        }

        public static Task CopyToAsync(Stream thisref, Stream destination)
        {
            var x = new Task(() => { int i = 0; i++; });
            return x;
        }

        public static Task CopyToAsync(Stream thisref, Stream destination, int bufferSize)
        {
            var x = new Task(() => { int i = 0; i++; });
            return x;
        }

        public static Task CopyToAsync(Stream thisref, Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            var x = new Task(() => { int i = 0; i++; });
            return x;
        }

        public static Task FlushAsync(Stream thisref)
        {
            var x = new Task(() => { });
            return x;
        }        
    }

    public class WebRequestStub
    {
        public static Task<Stream> GetRequestStreamAsync(WebRequest thisref)
        {
            var x = new Task<Stream>(() => new MemoryStream());
            return x;
        }

        public static Task<WebResponse> GetResponseAsync()
        {
            var x = new Task<WebResponse>(() => null);
            return x;
        }

    }

    public class DnsStub
    {
        public static Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress)
        {
            var x = new Task<IPAddress[]>(() => null);
            return x;
        }

        public static Task<IPAddress[]> GetHostAddressesAsync(IPAddress address)
        {
            var x = new Task<IPAddress[]>(() => null);
            return x;
        }

        public static Task<IPHostEntry> GetHostEntryAsync(IPAddress address)
        {
            var x = new Task<IPHostEntry>(() => null);
            return x;
        }
    }

    public class TaskFactoryStub
    {
        public static Task FromAsync(TaskFactory thisRef, Func<AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod, object state)
        {
            var x = new Task(() => { });
            return x;
        }
    }

    public class WebSocketStub
    {
        public static Task CloseAsync(WebSocket thisRef, WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            var x = new Task(() => { });
            return x;
        }
        
        public static Task<WebSocketReceiveResult> ReceiveAsync(WebSocket thisRef, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var x = new Task<WebSocketReceiveResult>(() => null);
            return x;
        }

        public static Task SendAsync(WebSocket thisRef, ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var x = new Task(() => { });
            return x;
        }
    }

    public static class HttpContentExtensionsStub
    {
        public static Task<T> ReadAsAsync<T>(this HttpContent content)
        {
            var x = new Task<T>(() => default(T));
            return x;
        }
        
        public static Task<T> ReadAsAsync<T>(this HttpContent content, IEnumerable<MediaTypeFormatter> formatters)
        {
            var x = new Task<T>(() => default(T));
            return x;
        }
        
        public static Task<T> ReadAsAsync<T>(this HttpContent content, IEnumerable<MediaTypeFormatter> formatters, IFormatterLogger formatterLogger)
        {
            var x = new Task<T>(() => default(T));
            return x;
        }
    }

    public class HttpClientStub
    {
        public static Task<HttpResponseMessage> DeleteAsync(HttpClient thisref, string requestUri)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> DeleteAsync(HttpClient thisref, Uri requestUri)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> DeleteAsync(HttpClient thisref, string requestUri, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> DeleteAsync(HttpClient thisref, Uri requestUri, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient thisref, string requestUri)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient thisref, Uri requestUri)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }
        
        public Task<HttpResponseMessage> GetAsync(HttpClient thisref, string requestUri, HttpCompletionOption completionOption)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient thisref, Uri requestUri, HttpCompletionOption completionOption)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient thisref, string requestUri, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient thisref, Uri requestUri, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient thisref, Uri requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient thisref, string requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<byte[]> GetByteArrayAsync(HttpClient thisref, string requestUri)
        {
            var x = new Task<byte[]>(() => null);
            return x;
        }

        public static Task<byte[]> GetByteArrayAsync(HttpClient thisref, Uri requestUri)
        {
            var x = new Task<byte[]>(() => new byte[10]);
            return x;
        }

        public static Task<Stream> GetStreamAsync(HttpClient thisref, Uri requestUri)
        {
            var x = new Task<Stream>(() => new MemoryStream());
            return x;
        }

        public static Task<Stream> GetStreamAsync(HttpClient thisref, string requestUri)
        {
            var x = new Task<Stream>(() => new MemoryStream());
            return x;
        }

        public static Task<string> GetStringAsync(HttpClient thisref, string requestUri)
        {
            var x = new Task<String>(() => "x");
            return x;
        }

        public static Task<string> GetStringAsync(HttpClient thisref, Uri requestUri)
        {
            var x = new Task<String>(() => "x");
            return x;
        }

        public static Task<HttpResponseMessage> PostAsync(HttpClient thisref, string requestUri, HttpContent content)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> PostAsync(HttpClient thisref, Uri requestUri, HttpContent content)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> PostAsync(HttpClient thisref, string requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> PostAsync(HttpClient thisref, Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> PutAsync(HttpClient thisref, string requestUri, HttpContent content)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> PutAsync(HttpClient thisref, Uri requestUri, HttpContent content)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> PutAsync(HttpClient thisref, string requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> PutAsync(HttpClient thisref, Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> SendAsync(HttpClient thisref, HttpRequestMessage request)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> SendAsync(HttpClient thisref, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> SendAsync(HttpClient thisref, HttpRequestMessage request, HttpCompletionOption completionOption)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }

        public static Task<HttpResponseMessage> SendAsync(HttpClient thisref, HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            var x = new Task<HttpResponseMessage>(() => null);
            return x;
        }
        
    }

    public class TaskStub
    {
        public static Task Delay(System.TimeSpan millisecondsDelay)
        {
            var x = new Task(() => { });
            return x;
        }

        public static Task Delay(int millisecondsDelay)
        {
            var x = new Task(() => { });
            return x;
        }

        //public static Task<Task> WhenAny(IEnumerable<Task> t)
        //{
        //    var x = new Task<Task>(() => null);
        //    return x;
        //}

        public static Task<Task> WhenAny(Task[] t)
        {
            var x = new Task<Task>(() => null);            
            return x;
        }

        public static Task<T> WhenAny<T>(IEnumerable<Task<T>> t)
        {
            var x = new Task<T>(() => default(T));            
            return x;
        }

        public static Task<T> WhenAny<T>(Task<T>[] t)
        {
            var x = new Task<T>(() => default(T));            
            return x;
        }

        public static Task WhenAll(IEnumerable<Task> t)
        {
            var x = new Task(() => { });
            return x;
        }

        public static Task WhenAll(Task[] t)
        {
            var x = new Task(() => { });
            return x;
        }

        public static Task<T> WhenAll<T>(IEnumerable<Task<T>> t)
        {
            var x = new Task<T>(() => default(T));
            return x;
        }

        public static Task<T> WhenAll<T>(Task<T>[] t)
        {
            var x = new Task<T>(() => default(T));
            return x;
        }

        public Task StartNew(TaskFactory thisRef, Action action)
        {
            var x = new Task(() => action());
            return x;
        }

        public static Task Run(Func<Task> function)
        {
            var v = function();
            return v;
        }

        public static Task Run(Action action)
        {
            var x = new Task(() => action());
            return x;
        }

        public static Task Run(Func<Task> action, CancellationToken cancellationToken)
        {
            var v = action();
            return v;
        }

        public static Task Run(Action action, CancellationToken cancellationToken)
        {
            var x = new Task(() => action());
            return x;
        }

        public static Task<TResult> Run<TResult>(Func<Task<TResult>> function)
        {
            var x = function();
            return x;
        }

        public static Task<TResult> Run<TResult>(Func<Task<TResult>> function, CancellationToken cancellationToken)
        {
            //var x = new Task<TResult>(() => default(TResult));
            var x = function();
            return x;
        }

        public static Task<TResult> StartNew<TResult>(TaskFactory thisRef, Func<TResult> function)
        {
            var x = new Task<TResult>(() => function());
            return x;
        }

    }
    
    public class CloudBlobStub
    {
        public static Task<bool> ExistsAsync(ICloudBlob thisref)
        {
            var x = new Task<bool>(() => false);
            return x;
        }
    }
    
    public class CloudBlobContainerStub
    {
        public static Task<bool> CreateIfNotExistsAsync(CloudBlobContainer thisref)
        {
            var x = new Task<bool>(() => false);
            return x;
        }
    }   

    public class CloudBlockBlobStub
    {
        public static Task UploadFromStreamAsync(CloudBlockBlob thisref, Stream Source)
        {
            var x = new Task(() => { });
            return x;
        }
    }

    public class StreamWriterStub
    {
        public static Task WriteAsync(StreamWriter thisref, string value)
        {
            var x = new Task(() => { });
            return x;
        }        
    }

    public class StreamReaderStub
    {
        public static Task ReadAsync(StreamReader thisref)
        {
            var x = new Task(() => { });
            return x;
        }
    }

    public class TextWriterStub
    {
        public static Task WriteAsync(TextWriter thisref, string value)
        {
            var x = new Task(() => { });
            return x;
        }
    }

    public class JobHostStub
    {
        public static Task CallAsync(JobHost thisref, MethodInfo method, IDictionary<string, object> arguments, CancellationToken cancellationToken = default(CancellationToken))
        {
            var x = new Task(() => { });
            return x;
        }

        public static Task StopAsync(JobHost thisref)
        {
            var x = new Task(() => { });
            return x;
        }
    }

    public class ServiceClientCredentialsStub
    {
        public static Task ProcessHttpRequestAsync(ServiceClientCredentials thisref, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var x = new Task(() => { });
            return x;
            //return Task.FromResult<object>(null);
        }
    }

    public static class AzureClientExtensionsStub
    {
        public static Task<AzureOperationResponse<TBody>> GetPutOrPatchOperationResultAsync<TBody>(this IAzureClient client, AzureOperationResponse<TBody> response, Dictionary<string, List<string>> customHeaders, CancellationToken cancellationToken) where TBody : class
        {
            var x = new Task<AzureOperationResponse<TBody>>(() =>  default(AzureOperationResponse<TBody>)  );
            return x;
        }

        public static Task<AzureOperationResponse> GetPostOrDeleteOperationResultAsync(this IAzureClient client, AzureOperationResponse response, Dictionary<string, List<string>> customHeaders, CancellationToken cancellationToken)
        {
            var x = new Task<AzureOperationResponse>(() => default(AzureOperationResponse));
            return x;
        }
    }

    public static class HttpContentStub
    {
        public static Task<Stream> ReadAsStreamAsync(HttpContent thisref)
        {
            var x = new Task<Stream>(() => null);
            return x;
        }

        public static Task<String> ReadAsStringAsync(HttpContent thisref)
        {
            var x = new Task<String>(() => null);
            return x;
        }
    }

    public static class IdentityStub
    {
        public static Task<AuthenticationResult> AcquireTokenAsync(AuthenticationContext thisref, string resource, string clientId, UserCredential userCredential)
        {
            var x = new Task<AuthenticationResult>(() => null);
            return x;
        }

        public static Task<AuthenticationResult> AcquireTokenSilentAsync(AuthenticationContext thisref, string resource, string clientId, UserIdentifier userId)
        {
            var x = new Task<AuthenticationResult>(() => null);
            return x;
        }

        public static Task<AuthenticationResult> AcquireTokenAsync(AuthenticationContext thisref, string resource, ClientAssertionCertificate clientCertificate)
        {
            var x = new Task<AuthenticationResult>(() => null);
            return x;
        }

        public static Task<AuthenticationResult> AcquireTokenAsync(AuthenticationContext thisref, string resource, ClientCredential clientCredential)
        {
            var x = new Task<AuthenticationResult>(() => null);
            return x;
        }
    }

    //public static class SecretManagerStub
    //{
    //    public static Task<ScriptSecrets> LoadSecretsAsync(SecretManager thisref, ScriptSecretsType type, string functionName, Func<string, ScriptSecrets> deserializationHandler)
    //    {
    //        var x = new Task<ScriptSecrets>(() => null);
    //        return x;
    //    }
    //}
}
