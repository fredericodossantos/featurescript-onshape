// Saca-rolamento linear LM8UU para alojamento passante de aproximadamente 15,10 mm.
// Uso: gere as duas pecas, imprima-as e use barra roscada M6 + porca externa no copo.
// Antes de colar, substitua o FeatureScript/imports abaixo pelos que o seu Feature Studio novo gerou.

FeatureScript 3083;
import(path : "onshape/std/geometry.fs", version : "3083.0");
import(path : "onshape/std/properties.fs", version : "3083.0");

function cylinder(context is Context, id is Id, a is Vector, b is Vector, radius is number) returns Query
{
    fCylinder(context, id, {
            "bottomCenter" : a * millimeter,
            "topCenter" : b * millimeter,
            "radius" : radius * millimeter
    });
    return qCreatedBy(id, EntityType.BODY);
}

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

function nameBody(context is Context, body is Query, label is string)
{
    setProperty(context, {
            "entities" : body,
            "propertyType" : PropertyType.NAME,
            "value" : label
    });
}

annotation { "Feature Type Name" : "Saca rolamento linear LM8UU" }
export const sacaRolamentoLinearLm8uu = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Diametro do alojamento" }
        isLength(definition.boreDiameter, { (millimeter) : [14, 15.1, 25] } as LengthBoundSpec);

        annotation { "Name" : "Diametro externo do tubo" }
        isLength(definition.housingDiameter, { (millimeter) : [18, 21, 35] } as LengthBoundSpec);

        annotation { "Name" : "Comprimento do copo" }
        isLength(definition.cupLength, { (millimeter) : [30, 52, 100] } as LengthBoundSpec);
    }
    {
        // Converte os inputs para numeros puros em mm uma unica vez.
        var boreDiameter = definition.boreDiameter / millimeter;
        var housingDiameter = definition.housingDiameter / millimeter;
        var cupLength = definition.cupLength / millimeter;

        // Dimensoes validadas contra o STL: alojamento medido em 15,10 mm.
        // A manga toca somente a casca externa do LM8UU, nao as esferas nem a gaiola.
        var rodClearanceDiameter = 6.6;       // barra/parafuso M6
        var sleeveDiameter = boreDiameter - 0.8;
        var sleeveLength = 18;
        var flangeDiameter = boreDiameter + 3.8;
        var flangeThickness = 3.5;

        // Peca 1: manga empurradora. Eixo X representa o eixo do rolamento.
        var pusher = cylinder(context, id + "pusherCore",
                vector(0, 0, 0), vector(sleeveLength, 0, 0), sleeveDiameter / 2);
        var pusherFlange = cylinder(context, id + "pusherFlange",
                vector(-flangeThickness, 0, 0), vector(0.5, 0, 0), flangeDiameter / 2);
        var pusherHole = cylinder(context, id + "pusherHole",
                vector(-flangeThickness - 1, 0, 0), vector(sleeveLength + 1, 0, 0), rodClearanceDiameter / 2);
        combine(context, id + "pusherFinish", pusher, [pusherFlange], [pusherHole]);
        nameBody(context, pusher, "01_Manga empurradora LM8UU");

        // Peca 2: copo receptor. Ele recebe ate dois LM8UU de 24 mm.
        // O contra-furo da boca encaixa sobre a ponta externa do tubo do carro.
        var cupOffsetY = 38;
        var cupWall = 3.5;
        var cupOuterDiameter = housingDiameter + 2 * cupWall;
        var receiverDiameter = boreDiameter + 1.4;
        var locatorDiameter = housingDiameter + 0.6;
        var locatorDepth = 3;
        var capThickness = 5;

        var cup = cylinder(context, id + "cupCore",
                vector(0, cupOffsetY, 0), vector(cupLength, cupOffsetY, 0), cupOuterDiameter / 2);
        var receiverCavity = cylinder(context, id + "cupCavity",
                vector(-1, cupOffsetY, 0), vector(cupLength - capThickness, cupOffsetY, 0), receiverDiameter / 2);
        var locatorCounterbore = cylinder(context, id + "cupLocator",
                vector(-1, cupOffsetY, 0), vector(locatorDepth, cupOffsetY, 0), locatorDiameter / 2);
        var cupRodHole = cylinder(context, id + "cupRodHole",
                vector(cupLength - capThickness - 1, cupOffsetY, 0), vector(cupLength + 1, cupOffsetY, 0), rodClearanceDiameter / 2);
        combine(context, id + "cupFinish", cup, [], [receiverCavity, locatorCounterbore, cupRodHole]);
        nameBody(context, cup, "02_Copo receptor LM8UU");
    });
