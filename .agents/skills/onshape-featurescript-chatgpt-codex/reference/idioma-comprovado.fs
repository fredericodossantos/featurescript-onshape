FeatureScript 3083;
import(path : "onshape/std/geometry.fs", version : "3083.0");
import(path : "onshape/std/properties.fs", version : "3083.0");

// =====================================================================
// IDIOMA COMPROVADO - modelo de referencia
//
// As funcoes auxiliares abaixo foram copiadas de cad/mesa_xy_pecas_fuso.fs
// (projeto cameraobscura), que regenera OK no Onshape (2026-09-21).
// O feature "Exemplo - bloco com furo" no fim e um exemplo minimo montado com
// essas funcoes para mostrar precondition, cotas(), guarda e booleanos.
//
// Regras visiveis aqui:
//   - numeros puros em mm; unidade so na fronteira (/ millimeter, * millimeter)
//   - fCylinder direto (sem sketch + extrude)
//   - UNION so com "tools" (alvo dentro do qUnion), sem "targets"
//   - SUBTRACTION com "targets" + "tools"; ferramenta estoura a parede (MARGEM)
//   - enum de parametro: export enum + annotation em cada valor
//   - guarda = throw regenError(...)
//   - ASCII puro, sem ternario, && / ||
// =====================================================================

annotation { "Name" : "Parafuso" }
export enum ParafusoExemplo
{
    annotation { "Name" : "M4" }
    M4,
    annotation { "Name" : "M3" }
    M3
}

const MARGEM = 1;   // quanto a ferramenta de corte estoura a parede que atravessa

// -------------------- primitivas --------------------

function makeBlock(context is Context, id is Id, x1 is number, y1 is number, z1 is number,
        x2 is number, y2 is number, z2 is number) returns Query
{
    fCuboid(context, id, {
            "corner1" : vector(x1, y1, z1) * millimeter,
            "corner2" : vector(x2, y2, z2) * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

function cylinder(context is Context, id is Id, a is Vector, b is Vector, radius is number) returns Query
{
    fCylinder(context, id, {
            "bottomCenter" : a * millimeter,
            "topCenter" : b * millimeter,
            "radius" : radius * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

// Cilindro centrado em "center", ao longo de "X", "Y" ou "Z".
function cylinderOnAxis(context is Context, id is Id, center is Vector, axis is string,
        diameter is number, length is number) returns Query
{
    var n;
    if (axis == "X")
    {
        n = vector(1, 0, 0);
    }
    else if (axis == "Y")
    {
        n = vector(0, 1, 0);
    }
    else
    {
        n = vector(0, 0, 1);
    }
    return cylinder(context, id, center - n * (length / 2), center + n * (length / 2), diameter / 2);
}

// Une "additions" ao alvo e depois subtrai "cuts". Continue usando "target"
// como Query do corpo resultante (o Id do boolean nao e um corpo novo).
function combine(context is Context, id is Id, target is Query, additions is array, cuts is array)
{
    if (size(additions) > 0)
    {
        opBoolean(context, id + "join", {
                "tools" : qUnion(concatenateArrays([[target], additions])),
                "operationType" : BooleanOperationType.UNION
        });
    }
    if (size(cuts) > 0)
    {
        opBoolean(context, id + "cut", {
                "targets" : target,
                "tools" : qUnion(cuts),
                "operationType" : BooleanOperationType.SUBTRACTION
        });
    }
}

// Nome + cor de uma vez: todo corpo passa por aqui.
function nomear(context is Context, body is Query, label is string, cor is Color)
{
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.NAME,
            "value" : label
    });
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.APPEARANCE,
            "value" : cor
    });
}

// -------------------- cotas: FONTE UNICA --------------------

function diaPassagem(p is ParafusoExemplo) returns number
{
    if (p == ParafusoExemplo.M3)
    {
        return 3.4;
    }
    return 4.5;
}

// Converte a definition (com unidade) em numeros puros em mm, num lugar so.
function cotas(d is map) returns map
{
    return {
        "largura" : d.largura / millimeter,
        "altura" : d.altura / millimeter,
        "espessura" : d.espessura / millimeter,
        "furo" : diaPassagem(d.parafuso)
    };
}

// -------------------- feature --------------------

annotation { "Feature Type Name" : "Exemplo - bloco com furo" }
export const exemploBlocoComFuro = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Parafuso" }
        definition.parafuso is ParafusoExemplo;

        annotation { "Name" : "Largura" }
        isLength(definition.largura, { (millimeter) : [10, 40, 200] } as LengthBoundSpec);

        annotation { "Name" : "Altura" }
        isLength(definition.altura, { (millimeter) : [5, 20, 200] } as LengthBoundSpec);

        annotation { "Name" : "Espessura" }
        isLength(definition.espessura, { (millimeter) : [2, 6, 50] } as LengthBoundSpec);

        annotation { "Name" : "Com aba", "Default" : true }
        definition.comAba is boolean;
    }
    {
        var c = cotas(definition);

        if (c.furo + 4 > c.altura)
        {
            throw regenError("Furo grande demais para a altura: aumente Altura ou use parafuso menor.");
        }

        // corpo principal: X = largura, Y = espessura, Z = altura
        var corpo = makeBlock(context, id + "corpo", -c.largura / 2, 0, 0, c.largura / 2, c.espessura, c.altura);

        var adds = [];
        if (definition.comAba)
        {
            // a aba mergulha 0,5 mm no corpo: sobreposicao positiva, nunca tangente
            adds = append(adds, makeBlock(context, id + "aba",
                    -c.largura / 2, c.espessura - 0.5, 0, c.largura / 2, c.espessura + 10, c.espessura));
        }

        // furo atravessa a espessura com MARGEM dos dois lados
        var furo = cylinderOnAxis(context, id + "furo", vector(0, c.espessura / 2, c.altura / 2), "Y",
                c.furo, c.espessura + 2 * MARGEM);

        combine(context, id + "bool", corpo, adds, [furo]);
        nomear(context, corpo, "Bloco com furo", color(0.16, 0.45, 0.80));
    });
