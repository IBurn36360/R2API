using R2API.Utils;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using RoR2.Networking;

namespace R2API {
    // ReSharper disable once InconsistentNaming
    [R2APISubmodule]
    public static class PrefabAPI {
        #region Loaded check
        //Maybe best to set up a base class or interface that does this automatically?
        public static bool Loaded {
            get {
                return IsLoaded;
            }
        }
        private static bool IsLoaded = false;
        #endregion

        private static bool needToRegister = false;
        private static GameObject parent;
        private static List<HashStruct> thingsToHash = new List<HashStruct>();

#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
                              /// <summary>
                              /// Duplicates a GameObject and leaves it in a "sleeping" state where it is inactive, but becomes active when spawned.
                              /// Also registers the clone to network if registerNetwork is not set to false.
                              /// Do not override the file, member, and line number parameters. They are used to generate a unique hash for the network ID.
                              /// </summary>
                              /// <param name="g">The GameObject to clone</param>
                              /// <param name="nameToSet">The name to give the clone (Should be unique)</param>
                              /// <param name="registerNetwork">Should the object be registered to network</param>
                              /// <returns>The GameObject of the clone</returns>
        public static GameObject InstantiateClone( this GameObject g, string nameToSet, bool registerNetwork = true, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0 ) {
            if( !IsLoaded ) {
                R2API.Logger.LogError( "PrefabAPI is not loaded. Please use [R2API.Utils.SubModuleDependency]" );
                return null;
            }
            GameObject prefab = MonoBehaviour.Instantiate<GameObject>(g, GetParent().transform);
            prefab.name = nameToSet;
            if( registerNetwork ) {
                RegisterPrefabInternal( prefab, file, member, line );
            }
            return prefab;
        }
        /// <summary>
        /// Registers a prefab so that NetworkServer.Spawn will function properly with it.
        /// Only will work on prefabs with a NetworkIdentity component.
        /// Is never needed for existing objects unless you have cloned them.
        /// Do not override the file, member, and line number parameters. They are used to generate a unique hash for the network ID.
        /// </summary>
        /// <param name="g">The prefab to register</param>
        public static void RegisterNetworkPrefab( this GameObject g, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0 ) {
            if( !IsLoaded ) {
                R2API.Logger.LogError( "PrefabAPI is not loaded. Please use [R2API.Utils.SubModuleDependency]" );
                return;
            }
            RegisterPrefabInternal( g, file, member, line );
        }
#pragma warning restore CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
        
        [R2APISubmoduleInit(Stage = InitStage.SetHooks)]
        internal static void SetHooks() {

        }

        [R2APISubmoduleInit(Stage = InitStage.UnsetHooks)]
        internal static void UnsetHooks() {

        }

        private static GameObject GetParent() {
            if( !parent ) {
                parent = new GameObject( "ModdedPrefabs" );
                MonoBehaviour.DontDestroyOnLoad( parent );
                parent.SetActive( false );

                On.RoR2.Util.IsPrefab += ( orig, obj ) => {
                    if( obj.transform.parent && obj.transform.parent.gameObject.name == "ModdedPrefabs" ) return true;
                    return orig( obj );
                };
            }

            return parent;
        }

        private struct HashStruct {
            public GameObject prefab;
            public string goName;
            public string callPath;
            public string callMember;
            public int callLine;
        }

        private static void RegisterPrefabInternal( GameObject prefab, string callPath, string callMember, int callLine ) {
            HashStruct h = new HashStruct
            {
                prefab = prefab,
                goName = prefab.name,
                callPath = callPath,
                callMember = callMember,
                callLine = callLine
            };
            thingsToHash.Add( h );
            SetupRegistrationEvent();
        }

        private static void SetupRegistrationEvent() {
            if( !needToRegister ) {
                needToRegister = true;
                RoR2.Networking.GameNetworkManager.onStartGlobal += RegisterClientPrefabsNStuff;
            }
        }

        private static NetworkHash128 nullHash = new NetworkHash128
        {
            i0 = 0,
            i1 = 0,
            i2 = 0,
            i3 = 0,
            i4 = 0,
            i5 = 0,
            i6 = 0,
            i7 = 0,
            i8 = 0,
            i9 = 0,
            i10 = 0,
            i11 = 0,
            i12 = 0,
            i13 = 0,
            i14 = 0,
            i15 = 0
        };

        private static void RegisterClientPrefabsNStuff() {
            foreach( HashStruct h in thingsToHash ) {
                if( (h.prefab.GetComponent<NetworkIdentity>() != null)) h.prefab.GetComponent<NetworkIdentity>().SetFieldValue<NetworkHash128>( "m_AssetId", nullHash );
                ClientScene.RegisterPrefab( h.prefab, NetworkHash128.Parse( MakeHash( h.goName + h.callPath + h.callMember + h.callLine.ToString() ) ) );
            }
        }

        private static string MakeHash(string s ) {
            MD5 hash = MD5.Create();
            byte[] prehash = hash.ComputeHash( Encoding.UTF8.GetBytes( s ) );
            hash.Dispose();
            StringBuilder sb = new StringBuilder();

            for(int i = 0; i < prehash.Length; i++ ) {
                sb.Append( prehash[i].ToString( "x2" ) );
            }

            return sb.ToString();
        }
    }
}
