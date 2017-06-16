using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SafetyAnalysis.Framework.Graphs;
using SafetyAnalysis.Util;
using SafetyAnalysis.TypeUtil;
using SafetyAnalysis.Purity;

namespace SafetyAnalysis.Purity.Summaries
{
    public class SyntheticMethodStub : MethodStubManager
    {
        private static SyntheticMethodStub _instance = null;

        public static SyntheticMethodStub GetInstance()
        {
            if(_instance == null)
                _instance = new SyntheticMethodStub();
            return _instance;
        }

        private SyntheticMethodStub() { }

        public bool HasSummary(TypeUtil.MethodInfo methodinfo)
        {            
            string qualtypename = methodinfo.DTypename;
            string methodname = methodinfo.Methodname;
            string signature = methodinfo.Sig;
            var typename = PhxUtil.RemoveAssemblyName(qualtypename);

            if (typename.Equals("System.String"))
                return true;
            if (typename.Equals("System.Convert"))
                return true;
            if (typename.Equals("System.Array")
                && methodname.Equals("CreateInstance"))            
                return true;
            //if (typename.Equals("System.Math"))
            //    return true;
            if ( typename.Equals("System.Guid"))
                return true;
            if (typename.Equals("System.Object") &&  methodname.Equals("ctor"))                
                return true;
            if (typename.Equals("System.Object") &&  methodname.Equals("GetType"))
                return true;
            if (typename.Equals("System.Object") &&  methodname.Equals("MemberwiseClone"))
                return true;
            if (typename.Equals("System.Object") &&  methodname.Equals("Equals"))
                return true;
            if (typename.Equals("System.Object") &&  methodname.Equals("GetHashCode"))
                return true;
            if (typename.Equals("System.Object") &&  methodname.Equals("ToString"))
                return true;
            if (methodname.Equals("ctor") && 
                TypeMethodInfoUtil.IsDelegateType(methodinfo.Typeinfo.Their, methodinfo.Typeinfo))
                return true;            
            if (typename.Equals("System.ValueType"))
                return true;
            if (typename.Equals("System.Enum"))
                return true;
            if (typename.Equals("System.Type"))
                return true;            
            if (typename.Equals("System.Text.StringBuilder"))
                return true;
            if (typename.Equals("System.TimeZone"))
                return true;            
            if (typename.Contains("System.SR") &&  methodname.Equals("GetString"))
                return true;
            if (typename.Contains("System.Globalization"))
                return true;           
            if ( typename.Contains("System.Reflection.Emit"))
                return true;
            if (typename.Contains("System.Runtime.Serialization"))
                return true;            
            if (typename.Equals("System.Threading.Interlocked"))                
                return false;
            if (typename.Contains("System.Threading.Tasks.TaskFactory"))
            {
                if (methodname.Contains("StartNew") && signature.Contains("([mscorlib]System.Action)[mscorlib]System.Threading.Tasks.Task"))
                    return false;
                if (methodname.Contains("StartNew") && signature.Contains("([mscorlib]System.Func`1)[mscorlib]System.Threading.Tasks.Task`1"))
                    return false;
            }
            if (typename.Contains("System.Threading.Tasks.Task"))
            {
                if (methodname.Equals("Run")) // && signature.Equals("([mscorlib]System.Action)[mscorlib]System.Threading.Tasks.Task"))
                    return false;
                //if (methodname.Contains("Run") && signature.Equals("([mscorlib]System.Func`1,[mscorlib]System.Threading.CancellationToken)[mscorlib]System.Threading.Tasks.Task"))
                //    return false;
                //if (methodname.Contains("Run") && signature.Equals("([mscorlib]System.Func`1,[mscorlib]System.Threading.CancellationToken)[mscorlib]System.Threading.Tasks.Task`1"))
                //    return false;
                //if (methodname.Contains("Run") && signature.Equals("([System.Runtime]System.Func`1,[mscorlib]System.Threading.CancellationToken)[mscorlib]System.Threading.Tasks.Task"))
                //    return false;
                //if (methodname.Contains("Run") && signature.Equals("([System.Runtime]System.Func`1,[mscorlib]System.Threading.CancellationToken)[mscorlib]System.Threading.Tasks.Task`1"))
                //    return false;                
            }
            //if (typename.Contains("System.Threading.Tasks.Task"))
            //    if ((methodname.Contains("WhenAny") || methodname.Contains("WhenAll") || methodname.Contains("Delay")))
            //        return false;
            if (typename.Contains("System.Threading.Tasks.Task"))
                if (!(methodname.Contains("WhenAny") || methodname.Contains("WhenAll") || methodname.Contains("Delay")))
                {
                    Console.WriteLine("Excluding: [{0}]{1}/{2}", typename, methodname, signature);
                    return true;
                }
            //if (typename.Equals("System.Threading.Tasks.Task"))
            //    if (!methodname.Contains("get_Result") && !methodname.Contains("TrySetResult"))
            //        return true;
            //if (typename.Equals("System.Threading.Tasks.Task`1"))
            //    if (!methodname.Contains("get_Result") && !methodname.Contains("TrySetResult"))
            //        return true;
            if ( typename.Equals("System.Reflection.FieldInfo")
                ||  typename.Equals("System.Reflection.PropertyInfo")
                ||  typename.Equals("System.Reflection.MemberInfo")
                ||  typename.Equals("System.Reflection.MethodInfo")
                ||  typename.Equals("System.Reflection.MethodBase")
                ||  typename.Equals("System.Reflection.ParameterInfo")
                ||  typename.Equals("System.Reflection.ConstructorInfo")
                ||  typename.Equals("System.Reflection.FieldInfo")                
                )
                return true;
            if (typename.Contains("System.IO"))
                return true;
            if (typename.Equals("System.IO.Stream") && methodname.Contains("ReadAsync"))
                return false;
            if (typename.Equals("System.IO.Stream") && methodname.Contains("WriteAsync"))
                return false;
            if (typename.Equals("System.IO.Stream") && methodname.Contains("CopyToAsync"))
                return false;
            if (typename.Equals("System.IO.Stream") && methodname.Contains("FlushAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("PutAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("SendAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("DeleteAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("PutAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("PostAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("GetAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("GetStringAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("GetStreamAsync"))
                return false;
            if (typename.Equals("System.Net.Http.HttpClient") && methodname.Contains("GetByteArrayAsync"))
                return false;
            if (typename.Equals("System.Threading.Tasks.Task") && methodname.Contains("Delay"))
                return false;
            // New
            if (typename.Equals("Microsoft.WindowsAzure.Storage.Blob.CloudBlobContainer") && methodname.Contains("ExistsAsync"))
                return false;

            if (typename.Equals("Microsoft.WindowsAzure.Storage.File.CloudFileDirectory") && methodname.Contains("CreateIfNotExistsAsync"))
                return false;

            if (typename.Equals("Microsoft.WindowsAzure.Storage.Blob.CloudBlockBlob") && methodname.Contains("UploadFromStreamAsync"))
                return false;

            if (typename.Equals("System.IO.StreamWriter") && methodname.Contains("WriteAsync"))
                return false;

            if (typename.Equals("System.IO.TextWriter") && methodname.Contains("WriteAsync"))
                return false;

            if (typename.Equals("Microsoft.Azure.WebJobs.JobHost") && methodname.Contains("CallAsync"))
                return false;

            if (typename.Equals("Microsoft.Azure.WebJobs.JobHost") && methodname.Contains("StopAsync"))
                return false;

            if (typename.Equals("Microsoft.Rest.ServiceClientCredentials") && methodname.Contains("ProcessHttpRequestAsync"))
                return false;

            if (typename.Equals("Microsoft.Rest.Azure.AzureClientExtensions") && methodname.Contains("GetPutOrPatchOperationResultAsync"))
                return false;

            if (typename.Equals("Microsoft.WindowsAzure.Storage.File.CloudFileDirectory") && methodname.Contains("GetPostOrDeleteOperationResultAsync"))
                return false;

            if (typename.Equals("System.Net.Http") && methodname.Contains("ReadAsStreamAsync"))
                return false;

            if (typename.Equals("System.Net.Http") && methodname.Contains("ReadAsStringAsync"))
                return false;

            if (typename.Equals("System.Net.Http") && methodname.Contains("ReadAsStringAsync"))
                return false;

            if (typename.Equals("Microsoft.IdentityModel.Clients.ActiveDirectory") && methodname.Contains("AcquireTokenAsync"))
                return false;

            if (typename.Equals("Microsoft.IdentityModel.Clients.ActiveDirectory") && methodname.Contains("AcquireTokenSilentAsync"))
                return false;
          
            return false;
        }

        public override PurityAnalysisData GetSummary(TypeUtil.MethodInfo methodinfo)
        {
            string qualtypename = methodinfo.DTypename;
            string methodname = methodinfo.Methodname;
            string sig = methodinfo.Sig;
            var typename = PhxUtil.RemoveAssemblyName(qualtypename);
            var qualifiedname = typename + "::" + methodname;

            var pureData = SummaryTemplates.CreatePureData();

            //if (typename.Equals("System.Math"))
            //{
            //    //skip. all methods are pure.
            //    return pureData;
            //}
            //else 
                if (typename.Equals("System.Array") && methodname.Equals("CreateInstance"))
            {
                var outData = SummaryTemplates.CreatePureData();
                SummaryTemplates.MakeAllocator(outData, "[mscorlib]System.Array", methodname);
                return outData;
            }
            else if (typename.Equals("System.String"))
            {
                //this is a string method which is a pure functional implementation
                //so create a new object if the destination operand is a pointer operand                
                var outData = SummaryTemplates.CreatePureData();
                SummaryTemplates.MakeAllocator(outData, "[mscorlib]System.String", methodname);
                return outData;
            }
            else if (typename.Equals("System.Convert"))
            {
                var outData = SummaryTemplates.CreatePureData();
                SummaryTemplates.MakeAllocator(outData, "[mscorlib]System.String", methodname);
                return outData;
            }
            else if (typename.Equals("System.Enum"))
            {
                //system.Enum is pure.                
                return pureData;
            }
            else if (typename.Equals("System.Guid"))
            {
                //system.Guid is pure.                
                return pureData;
            }
            else if (typename.Equals("System.Type"))
            {
                if (methodname.Equals("InvokeMember"))
                {
                    //pollute the global object
                    var outData = SummaryTemplates.CreatePureData();
                    SummaryTemplates.WriteGlobalObject(outData, NamedField.New("InvokeMember", "[mscorlib]System.Type"));
                    return outData;
                }
                else
                {
                    //all other calls are pure (observationally).
                    return pureData;
                }
            }
            else if (typename.Equals("System.Text.StringBuilder"))
            {
                return pureData;
            }
            else if (typename.Equals("System.TimeZone"))
            {
                return pureData;
            }
            else if (typename.Equals("System.Object") && methodname.Equals("ctor"))
            {
                return pureData;
            }
            else if (typename.Equals("System.Object") && methodname.Equals("GetType"))
            {
                return pureData;
            }
            else if (typename.Equals("System.Object") && methodname.Equals("MemberwiseClone"))
            {
                //make return value point-to this vertex (over-approximation)
                var outData = SummaryTemplates.CreatePureData();
                var thisVertex = ParameterHeapVertex.New(1, "this");
                var retVertex = ReturnVertex.GetInstance();

                outData.OutHeapGraph.AddVertex(thisVertex);
                outData.OutHeapGraph.AddVertex(retVertex);

                var edge = new InternalHeapEdge(retVertex, thisVertex, null);
                outData.OutHeapGraph.AddEdge(edge);
                return outData;
            }
            else if (typename.Equals("System.Object") && methodname.Equals("Equals"))
            {
                //pure
                return pureData;
            }
            else if (typename.Equals("System.Object") && methodname.Equals("GetHashCode"))
            {
                //pure
                return pureData;
            }
            else if (typename.Equals("System.Object") && methodname.Equals("ToString"))
            {
                //supposed to return a newly created string object and also be pure.
                var outData = SummaryTemplates.CreatePureData();
                SummaryTemplates.MakeAllocator(outData, "[mscorlib]System.String", methodname);
                return outData;
            }
            else if (methodname.Equals("ctor") 
                && TypeMethodInfoUtil.IsDelegateType(methodinfo.Typeinfo.Their, methodinfo.Typeinfo))
            {
                //supposed to return a newly created delegate and also be pure.
                var outData = SummaryTemplates.CreatePureData();
                SummaryTemplates.MakeAllocator(outData, qualtypename, methodname);
                return outData;
            }
            else if (typename.Equals("System.SR") && methodname.Equals("GetString"))
            {
                //consider this as pure
                return pureData;
            }
            else if (typename.Equals("System.ValueType"))
            {
                //all the methods here are supposed to be pure.
                return pureData;
            }
            else if (typename.Contains("System.Globalization"))
            {
                //consider all the methods as pure.
                return pureData;
            }
            else if (typename.Contains("System.IO"))
            {
                var outData = SummaryTemplates.CreatePureData();
                SummaryTemplates.MakeAllocator(outData, "[mscorlib]System.String", methodname);
                SummaryTemplates.WriteGlobalObject(outData, NamedField.New(qualifiedname, null));
                return outData;
            }
            else if (typename.Contains("System.Reflection.Emit"))
            {
                var outData = SummaryTemplates.CreatePureData();
                SummaryTemplates.WriteGlobalObject(outData, NamedField.New(qualifiedname, null));
                return outData;
            }
            else if (typename.Contains("System.Runtime.Serialization"))
            {
                var outData = SummaryTemplates.CreatePureData();
                SummaryTemplates.WriteGlobalObject(outData, NamedField.New(qualifiedname, null));
                return outData;
            }
            else if (typename.Contains("System.Threading")
                && !typename.Contains("System.Threading.Interlocked"))
                //&& !typename.Contains("Tasks.Task"))
            {
                if (methodname.Contains("get_"))
                {
                    //consider this as pure.  
                    return pureData;
                }
                //else if (methodname.Contains("SetResult") && typename.Contains("Tasks.Task"))
                //{
                //    var newdata = SummaryTemplates.CreatePureData();
                //    var thisv = ParameterHeapVertex.New(1, "this");
                //    var param2 = ParameterHeapVertex.New(2, "value");
                //    newdata.OutHeapGraph.AddVertex(thisv);
                //    newdata.OutHeapGraph.AddVertex(param2);

                //    var field = NamedField.New("m_result", null);
                //    var edge = new InternalHeapEdge(thisv, param2, field);
                //    newdata.OutHeapGraph.AddEdge(edge);
                //    return newdata;
                //}
                else
                {
                    //considering all the methods here as impure.
                    var outData = SummaryTemplates.CreatePureData();
                    SummaryTemplates.WriteGlobalObject(outData, NamedField.New(qualifiedname, null));
                    return outData;
                }
            }
            else if ((typename.Equals("System.Reflection.FieldInfo")
                || typename.Equals("System.Reflection.PropertyInfo")
                || typename.Equals("System.Reflection.MemberInfo")
                || typename.Equals("System.Reflection.MethodInfo")
                || typename.Equals("System.Reflection.MethodBase")
                || typename.Equals("System.Reflection.ParameterInfo")
                || typename.Equals("System.Reflection.ConstructorInfo")
                || typename.Equals("System.Reflection.FieldInfo")
                ))
            {
                if (methodname.Contains("Get") || methodname.Contains("get_"))
                {
                    //considered as pure
                    return pureData;
                }
                else if (methodname.Contains("Set")
                    || methodname.Contains("set_")
                    || methodname.Contains("Invoke"))
                {
                    //pollute the global object
                    var outData = SummaryTemplates.CreatePureData();
                    SummaryTemplates.WriteGlobalObject(outData, NamedField.New(qualifiedname, null));
                    return outData;
                }
                else
                {
                    //assume all other calls are pure (observationally). These are calls like MakeGeneric..                    
                    return pureData;
                }
            }
            else
            {
                if (this.HasSummary(methodinfo))
                    throw new SystemException("Cannot construct summary for predefined method: " + qualifiedname);
                else
                    throw new SystemException(qualifiedname + " is  not a predefined method");
            }
        }
    }
}
