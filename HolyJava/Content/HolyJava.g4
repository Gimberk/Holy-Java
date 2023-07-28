grammar HolyJava;

program: line* EOF;

line: statement;

statement: (varKeywords | funcKeywords);

varKeywords: (varReassignment | varAssignment | varDeclaration);
funcKeywords: (funcDefinition | funcCall);

varReassignment: IDENTIFIER '=' expression ';';
varAssignment: varParameter '=' expression ';';
varDeclaration: varParameter ';';
varParameter: varType IDENTIFIER;
varType: PRIMATIVE_INT | PRIMATIVE_STRING | PRIMATIVE_FLOAT | PRIMATIVE_BOOL;

/*
func MyFunction (int age, string name) -> int 
{
	Stuff
}
*/

funcDefinition: 'func' IDENTIFIER '(' (varDeclaration? (',' varDeclaration)*)? ')' (ARROW_OP varType)? block;
funcCall: IDENTIFIER '(' (expression (',' expression)*)? ')' ';';

expression
: constant								#constantExpression
| IDENTIFIER							#identifierExpression
| funcCall								#funcCallExpression
| expression multOp expression          #multiplicativeExpression
| expression comparisonOps expression   #comparisonExpression
| expression addOp expression	        #additiveExpression
;

multOp: '*' | '/';
addOp: '+' | '-';
comparisonOps: '<' | '>' | '<=' | '>=' | '==' | '!=';
constant: INT | STRING | FLOAT | BOOL | NULL;

ARROW_OP: '->';

PRIMATIVE_INT: 'int';
PRIMATIVE_FLOAT: 'float';
PRIMATIVE_STRING: 'string';
PRIMATIVE_BOOL: 'bool';

INT: '-'? [0-9]+;
FLOAT: '-'? [0-9]+ '.' [0-9]+;
STRING: ('"' ~'"'* '"') | ('\'' ~'\''* '\'');
BOOL: 'true' | 'false';
NULL: 'null';

block: '{' line* '}';
IDENTIFIER: [a-zA-Z@_] [a-zA-Z0-9_]*;
WS: [ \t\r\n]+ -> skip;