using System;
using UnityEngine;

/// <summary>
/// Contenido del libro de la intro (INTRO.1 / A7): imagen de apertura (libro cerrado) y las 6 páginas del guion
/// (Docs/guiones/Guion-Intro.md §2).
/// ASSET: Assets/_Game/Data/Intro/IntroBook.asset (lo crea/actualiza Plomo y Paja → Intro → Construir escena del libro;
/// los textos se copian EXACTOS del guion, no se editan a mano).
/// Cualquier textura o id que falte deja un reemplazo visible (rectángulo rojo con el título, gris "ILUSTRACIÓN 0N",
/// papel crema, silencio).
/// </summary>
[CreateAssetMenu(fileName = "IntroBook", menuName = "Plomo y Paja/Intro Book")]
public class IntroBookData : ScriptableObject
{
    [Serializable]
    public class Page
    {
        [Tooltip("Id de la voz del narrador en la AudioLibrary (narrador_intro_0N)")]
        public string clipId;
        [TextArea(2, 5)] public string text;
        public Texture2D illustration;
        [Tooltip("Rótulo del reemplazo si falta la ilustración")]
        public string illustrationLabel;
    }

    [Header("Apertura")]
    [Tooltip("Título del reemplazo si falta libro_cerrado (la imagen real ya lo trae pintado)")]
    public string coverTitle = "PLOMO Y PAJA";
    [Tooltip("Art/Intro/libro_cerrado: el libro cerrado sobre la mesa, a pantalla completa")]
    public Texture2D closedBookTexture;
    [Tooltip("Art/Intro/hoja_papel: fondo de cada página y dorso de la hoja que gira")]
    public Texture2D paperTexture;

    [Header("Páginas (en orden)")]
    public Page[] pages = new Page[0];

    [Header("Audio (ids de la AudioLibrary)")]
    public string openSfx = "sfx_libro_abrir";
    [Tooltip("Grupo de variantes sfx_pagina_01..03 (se elige una al azar)")]
    public string pageSfx = "sfx_pagina";
    public string music = "musica_intro";
    public string roomAmbience = "amb_intro_cuarto";
    public string rain = "lluvia_loop";

    [Header("Salida")]
    [Tooltip("Escena del Nivel 1 (debe estar en Build Settings)")]
    public string nextScene = "NivelPorky";
}
