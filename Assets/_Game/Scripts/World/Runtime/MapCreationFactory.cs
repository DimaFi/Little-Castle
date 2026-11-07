using System;
using LittleCastle.World.Environment;
using UnityEngine;

namespace LittleCastle.World
{
    /// <summary>Plain new-map settings shared by editor and future runtime interfaces. Distances are metres.</summary>
    [Serializable]
    public sealed class MapCreationOptions
    {
        public int widthMetres=128;
        public int lengthMetres=128;
        public float baseHeight;
        public float minimumHeight=-4;
        public float maximumHeight=12;
        /// <summary>Empty generates a fresh map-GUID. An explicit ID must be unique in the caller's map library.</summary>
        public string mapId;
    }

    /// <summary>
    /// Creates a flat, independent authored document without scenes, assets or Unity objects.
    /// The 1 m height lattice uses a sparse constant base: creating a large empty map does not
    /// allocate a dense height grid, terrain mesh, generated world or presentation chunks.
    /// </summary>
    public static class MapCreationFactory
    {
        public const int MinimumDimensionMetres=32;
        public const int MaximumDimensionMetres=512;
        public const int MaximumMapIdLength=64;
        public const float HeightSampleSpacingMetres=1f;

        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool ValidExplicitId(string id)
        {
            if(id.Length>MaximumMapIdLength||!char.IsLetterOrDigit(id[0]))return false;
            foreach(char c in id)if(!char.IsLetterOrDigit(c)&&c!='-'&&c!='_')return false;
            return true;
        }

        /// <summary>
        /// Side-effect-free validation; does not generate IDs. mapIdExists is an optional
        /// caller-owned library lookup. A blank ID requests generation, not an existing map.
        /// </summary>
        public static bool TryValidate(MapCreationOptions options,out string error,Predicate<string> mapIdExists=null)
        {
            if(options==null){error="Не заданы параметры новой карты.";return false;}
            if(options.widthMetres<MinimumDimensionMetres||options.widthMetres>MaximumDimensionMetres)
            {error="Ширина карты должна быть от 32 до 512 м.";return false;}
            if(options.lengthMetres<MinimumDimensionMetres||options.lengthMetres>MaximumDimensionMetres)
            {error="Длина карты должна быть от 32 до 512 м.";return false;}
            if(!Finite(options.minimumHeight)||!Finite(options.maximumHeight)||options.minimumHeight>=options.maximumHeight)
            {error="Пределы высоты должны быть конечными числами; минимум должен быть ниже максимума.";return false;}
            if(!Finite(options.baseHeight)||options.baseHeight<options.minimumHeight||options.baseHeight>options.maximumHeight)
            {error="Высота ровной земли должна находиться между минимумом и максимумом.";return false;}
            if(!string.IsNullOrEmpty(options.mapId))
            {
                if(!ValidExplicitId(options.mapId))
                {error="ID карты: до 64 букв, цифр, дефисов или подчёркиваний; первый символ — буква или цифра.";return false;}
                if(mapIdExists!=null&&mapIdExists(options.mapId))
                {error="Карта с таким ID уже существует. Выберите другой ID или оставьте поле пустым.";return false;}
            }
            error=null;return true;
        }

        /// <summary>
        /// Creates a new independent document, centered on world origin. The supplied options
        /// are never changed. A caller accepting explicit IDs should supply its library lookup;
        /// the caller also owns registration/persistence, including any concurrent reservation.
        /// </summary>
        public static MapAuthoringDocument Create(MapCreationOptions options,Predicate<string> mapIdExists=null)
        {
            if(options==null)throw new ArgumentNullException(nameof(options));
            if(!TryValidate(options,out string error,mapIdExists))throw new ArgumentException(error,nameof(options));
            string id=options.mapId;
            if(string.IsNullOrEmpty(id))
            {
                id=null;
                for(int attempt=0;attempt<32;attempt++)
                {
                    string candidate="map-"+Guid.NewGuid().ToString("N");
                    if(mapIdExists==null||!mapIdExists(candidate)){id=candidate;break;}
                }
                if(id==null)throw new InvalidOperationException("Не удалось выделить свободный ID новой карты.");
            }
            var origin=new Vector2(-options.widthMetres*.5f,-options.lengthMetres*.5f);
            return new MapAuthoringDocument
            {
                mapId=id,
                bounds=new Vector4(origin.x,origin.y,options.widthMetres,options.lengthMetres),
                heightField=new MapHeightFieldData
                {
                    origin=origin,spacing=HeightSampleSpacingMetres,
                    minimumHeight=options.minimumHeight,maximumHeight=options.maximumHeight,baseHeight=options.baseHeight
                },
                environment=new MapEnvironmentData(),
                riverNetwork=new MapRiverNetworkData()
            };
        }
    }
}
