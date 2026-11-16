using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VDF.EditorTools
{
    /// <summary>
    /// Borra los materiales y mallas generados que ya no usa nadie.
    ///
    /// Por qué hace falta: el tablero se construye por código y crea un material por cada
    /// pareja de color y acabado. Eso está bien mientras los colores no cambian, pero al
    /// afinar el aspecto contra la foto del módulo real cada ajuste dejaba atrás los
    /// materiales de la versión anterior. En una tarde se acumularon 157 materiales de los
    /// que solo 61 estaban en uso, y el editor se volvió lento de cargar.
    ///
    /// No se borra nada que esté referenciado: se recorren las escenas, los prefabs y los
    /// propios materiales buscando el GUID de cada uno.
    /// </summary>
    public static class VdfMenuLimpiar
    {
        [MenuItem("VDF/Limpiar materiales y mallas sin usar")]
        public static void Limpiar()
        {
            var candidatos = new List<string>();
            candidatos.AddRange(Buscar("t:Material", "Assets/_VDF/Materials"));
            candidatos.AddRange(Buscar("t:Mesh", "Assets/_VDF/Mallas"));

            // Quién puede estar usándolos.
            var usuarios = new List<string>();
            usuarios.AddRange(Buscar("t:Scene", "Assets/_VDF/Scenes"));
            usuarios.AddRange(Buscar("t:Prefab", "Assets/_VDF/Prefabs"));
            usuarios.AddRange(Buscar("t:Material", "Assets/_VDF/Materials"));

            var usados = new HashSet<string>();
            for (int i = 0; i < usuarios.Count; i++)
                foreach (var dependencia in AssetDatabase.GetDependencies(usuarios[i], true))
                    usados.Add(dependencia);

            int borrados = 0;
            for (int i = 0; i < candidatos.Count; i++)
            {
                if (usados.Contains(candidatos[i])) continue;
                if (AssetDatabase.DeleteAsset(candidatos[i])) borrados++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VDF] Limpieza: " + borrados + " assets generados sin usar, de "
                      + candidatos.Count + " revisados.");
        }

        private static List<string> Buscar(string filtro, string carpeta)
        {
            var rutas = new List<string>();
            if (!AssetDatabase.IsValidFolder(carpeta)) return rutas;

            foreach (var guid in AssetDatabase.FindAssets(filtro, new[] { carpeta }))
                rutas.Add(AssetDatabase.GUIDToAssetPath(guid));
            return rutas;
        }
    }
}
