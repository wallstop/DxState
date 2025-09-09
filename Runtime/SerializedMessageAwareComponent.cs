namespace WallstopStudios.DxState
{
    using global::DxMessaging.Unity;
    using UnityEngine;
    using UnityHelpers.Core.Attributes;
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
    using Sirenix.Serialization;
#endif

#if ODIN_INSPECTOR
    [ShowOdinSerializedPropertiesInInspector]
#endif
    public abstract class SerializedMessageAwareComponent
        : MessageAwareComponent,
            ISerializationCallbackReceiver
#if ODIN_INSPECTOR
            ,
            ISupportsPrefabSerialization
#endif
    {
#if ODIN_INSPECTOR
        public SerializationData SerializationData
        {
            get => _serializationData;
            set => _serializationData = value;
        }
#endif

        [SerializeField]
        protected bool _showDebug;

        protected override void Awake()
        {
            this.AssignRelationalComponents();
            this.ValidateAssignments();
            base.Awake();
        }

#if ODIN_INSPECTOR
        [SerializeField]
        [HideInInspector]
        protected SerializationData _serializationData;
#endif

        public void OnBeforeSerialize()
        {
#if ODIN_INSPECTOR
            if (this == null)
            {
                return;
            }

            UnitySerializationUtility.SerializeUnityObject(this, ref _serializationData);
#endif
        }

        public void OnAfterDeserialize()
        {
#if ODIN_INSPECTOR
            if (this == null)
            {
                return;
            }

            UnitySerializationUtility.DeserializeUnityObject(this, ref _serializationData);
#endif
        }
    }
}
