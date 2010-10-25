/* Author: Ryan Westphal

*/

$(function() {
  /*!
   * \brief Report on the validity of restd input
   * \return a valid restd JavaScript object or null
   *
   * If there are no critical errors,
   * returned object will have at least headerSize, blockSize, metaSize &
   * data properties. The data property will be an array, the rest are numbers.
   */
  function report(outputResults) {
    // Check for input
    if ($("#restdInput").val() == "") {
      $("#restdOutput").val("Paste the full contents of a restd file into the top text area.\n");
      return null;
    }

    // Check for JSON
    var output = "";
    var inputObj = null;

    try {
      inputObj = $.parseJSON($("#restdInput").val());
      output += "Input is valid JSON.\n";
    } catch (e) {
      output += "Input is not valid JSON.\n";
      output += e.toString() + "\n";
      inputObj = null;
    }

    // Check for object
    if (inputObj != null) {
      if ($.isPlainObject(inputObj)) {
        output += "Input is a plain object.\n";
      } else {
        output += "Input is not a plain object, it must start with { and end with }.\n";
        inputObj = null;
      }
    }

    // Check the restd header
    if (inputObj != null) {
      if (inputObj.headerSize !== undefined) {
        output += "headerSize: " + inputObj.headerSize + " bytes\n";
      } else {
        output += "headerSize not defined, assuming default of 64 bytes\n";
        inputObj.headerSize = 64;
      }
      if (inputObj.blockSize !== undefined) {
        output += "blockSize: " + inputObj.blockSize + " bytes\n";
      } else {
        output += "blockSize not defined, assuming default of -1 (not blocked)\n";
        inputObj.blockSize = -1;
      }
      if (inputObj.metaSize !== undefined) {
        output += "metaSize: " + inputObj.metaSize + " bytes\n";
      } else {
        output += "metaSize not defined, assuming default of 0 bytes\n";
        inputObj.metaSize = 0;
      }
    }

    // Check for valid restd size vs. headerSize
    if (inputObj != null) {
      if ($("#restdInput").val().length > inputObj.headerSize) {
        output += "Input is correctly larger than headerSize\n";
      } else {
        output += "Input is not clean, input text is not larger than headerSize\n";
      }
    }

    // Check for other keys
    if (inputObj != null) {
      for (var restdProp in inputObj) {
        switch (restdProp) {
          case "headerSize":
          case "blockSize":
          case "metaSize":
          case "data":
            // known
            break;
          default:
            output += "Extra header property found: " + restdProp + ", ignoring\n";
            break;
        }
      }
    }

    // Check for data
    if (inputObj != null) {
      if (inputObj.data !== undefined) {
        output += "Input has a data property defined.\n";
      } else {
        output += "Input is not clean, no data property defined.\n";
        inputObj.data = [null];
      }
    }

    // Check that it's an array
    if (inputObj != null) {
      if ($.isArray(inputObj.data)) {
        output += "Input data is an array.\n";
      } else {
        output += "Input is not a valid restd file, the data property is not an array.\n";
        inputObj = null;
      }
    }

    // Check that each object in data is a plain object
    for (var i = 0; inputObj != null && i < inputObj.data.length; i++) {
      if (i == inputObj.data.length - 1) {
        if (inputObj.data[i] == null) {
          output += "data properly ends with a null.\n";
        } else {
          output += "Input is not clean, data does not end with a null.\n";
        }
      } else {
        if (!$.isPlainObject(inputObj.data[i]) && inputObj.data[i] != null) {
          output += "Input is not a valid restd file, data at index " + i + " is not a plain object or null.\n";
          output += i + ": " + inputObj.data[i] + "\n";
          inputObj = null;
          break;
        }
      }
    }

    if (inputObj != null) {
      output += "Input is either a valid restd file or can be made valid!\n";
    } else {
      output += "Input is not valid enough to be fixed by wellrestd\n";
    }

    if (outputResults) {
      $("#restdOutput").val(output);
    }

    return inputObj;
  }

  $("#report button").click(function() {
    report(true);
  });

  function blocksizeSet(restdObj, blockSize) {
    var i, output = "{";

    if (isNaN(blockSize)) {
      $("#restdOutput").val("Please provide a blockSize to set.");
      return;
    }

    if (blockSize == -1) {
      delete restdObj.blockSize;
    } else {
      restdObj.blockSize = blockSize;
    }

    if (restdObj.metaSize == 0) {
      delete restdObj.metaSize;
    }

    // Determine a good headerSize
    var isDefaultHeaderSize = restdObj.headerSize == 64;
    restdObj.headerSize = 0;
    for (var restdProp in restdObj) {
      switch (restdProp) {
        case "headerSize":
          // Ignore, we're calculating this one
          break;
        case "data":
          // No value in header, just prop name + :[
          restdObj.headerSize += 8;
          break;
        default:
          // 4 = quotes around prop name, colon & comma
          restdObj.headerSize += restdProp.length + 4 +
            stringify(restdObj[restdProp]).length;
          break;
      }
    }

    if (isDefaultHeaderSize) {
      if (restdObj.headerSize < 64) {
        delete restdObj.headerSize;
      } else {
        isDefaultHeaderSize = false;
      }
    }

    if (!isDefaultHeaderSize) {
      restdObj.headerSize += 10 + 4 + restdObj.headerSize.toString().length;
      // Combat new headerSize increasing character length,
      // there is a more specific way to do this...
      restdObj.headerSize += 2;
    }

    for (var restdProp in restdObj) {
      switch (restdProp) {
        case "data":
          // Ignore, we will add it to the end
          break;
        default:
          output += '"' + restdProp + '":' +
            stringify(restdObj[restdProp]) + ',';
          break;
      }
    }

    output += '"data":[';

    var headerSize = restdObj.headerSize || 64;
    for (i = output.length; i < headerSize; i++) {
      output += " ";
    }

    for (var objIdx = 0; objIdx < restdObj.data.length; objIdx++) {
      var addObj = objIdx < restdObj.data.length - 1 || restdObj.data[objIdx] != null;
      if (addObj) {
        var objStr = stringify(restdObj.data[objIdx]) + ",";
        if (blockSize != -1 && objStr.length > blockSize) {
          $("#restdOutput").val("data at index " + objIdx + " is longer than blockSize");
          return;
        }

        output += objStr;
        for (i = objStr.length; i < blockSize; i++) {
          output += " ";
        }
      }
    }
    
    output += "null]}";
    $("#restdOutput").val(output);
  };

  $("#make button").click(function() {
    var restdObj = report(false);
    if (restdObj == null) {
      report(true);
      return;
    }

    blocksizeSet(restdObj, restdObj.blockSize);
  });

  $("#blocksizeSet button").click(function() {
    var restdObj = report(false);
    if (restdObj == null) {
      report(true);
      return;
    }

    var blockSize = parseInt($("#blocksizeSet input").val());
    if (isNaN(blockSize)) {
      $("#restdOutput").val("Please provide a blockSize to set.");
      return;
    }

    blocksizeSet(restdObj, blockSize);
  });

  $("#blocksizeTucked button").click(function() {
    var restdObj = report(false);
    if (restdObj == null) {
      report(true);
      return;
    }

    var blockSize = 0;

    for (var i = 0; i < restdObj.data.length; i++) {
      var objStr = stringify(restdObj.data[i]) + ",";
      blockSize = Math.max(blockSize, objStr.length);
    }

    blocksizeSet(restdObj, blockSize);
  });

  $("#blocksizeFluffed button").click(function() {
    var restdObj = report(false);
    if (restdObj == null) {
      report(true);
      return;
    }

    var fluffSize = parseInt($("#blocksizeFluffed input").val());
    if (isNaN(fluffSize)) {
      $("#restdOutput").val("Please provide a fluff amount.");
      return;
    }

    var blockSize = 0;

    for (var i = 0; i < restdObj.data.length; i++) {
      var objStr = stringify(restdObj.data[i]) + ",";
      blockSize = Math.max(blockSize, objStr.length);
    }

    blockSize += fluffSize;

    blocksizeSet(restdObj, blockSize);
  });

  $("#fluff button").click(function() {
    var restdObj = report(false);
    if (restdObj == null) {
      report(true);
      return;
    }

    var fluffSize = parseInt($("#fluff input").val());
    if (isNaN(fluffSize)) {
      $("#restdOutput").val("Please provide a fluff amount.");
      return;
    }

    var blockSize = restdObj.blockSize;
    if (blockSize != -1) {
      blockSize += fluffSize;
    }

    blocksizeSet(restdObj, blockSize);
  });

  $("#vacuum button").click(function() {
    var restdObj = report(false);
    if (restdObj == null) {
      report(true);
      return;
    }

    // Remove nulls
    restdObj.data = $.map(restdObj.data, function(obj) { return obj; });

    blocksizeSet(restdObj, restdObj.blockSize);
  });


  $("#cmdCopy").click(function() {
    $("#restdInput").val($("#restdOutput").val());
  });



  /*
  The stringify method from json.org.
  
  http://www.JSON.org/json2.js
  2010-03-20

  Public Domain.

  NO WARRANTY EXPRESSED OR IMPLIED. USE AT YOUR OWN RISK.
  */

  function f(n) {
    return n < 10 ? '0' + n : n;
  }

  if (typeof Date.prototype.toJSON !== 'function') {
    Date.prototype.toJSON = function(key) {
      return isFinite(this.valueOf()) ? this.getUTCFullYear() + '-' + f(this.getUTCMonth() + 1) + '-' + f(this.getUTCDate()) + 'T' + f(this.getUTCHours()) + ':' + f(this.getUTCMinutes()) + ':' + f(this.getUTCSeconds()) + 'Z' : null;
    };

    String.prototype.toJSON = Number.prototype.toJSON = Boolean.prototype.toJSON = function(key) {
      return this.valueOf();
    };
  }

  var cx = /[\u0000\u00ad\u0600-\u0604\u070f\u17b4\u17b5\u200c-\u200f\u2028-\u202f\u2060-\u206f\ufeff\ufff0-\uffff]/g,
        escapable = /[\\\"\x00-\x1f\x7f-\x9f\u00ad\u0600-\u0604\u070f\u17b4\u17b5\u200c-\u200f\u2028-\u202f\u2060-\u206f\ufeff\ufff0-\uffff]/g,
        gap,
        indent,
        meta = {
          '\b': '\\b',
          '\t': '\\t',
          '\n': '\\n',
          '\f': '\\f',
          '\r': '\\r',
          '"': '\\"',
          '\\': '\\\\'
        },
        rep;

  function quote(string) {
    escapable.lastIndex = 0;
    return escapable.test(string) ? '"' + string.replace(escapable, function(a) {
      var c = meta[a];
      return typeof c === 'string' ? c : '\\u' + ('0000' + a.charCodeAt(0).toString(16)).slice(-4);
    }) + '"' : '"' + string + '"';
  }

  function str(key, holder) {
    var i, k, v, length, mind = gap, partial, value = holder[key];

    if (value && typeof value === 'object' && typeof value.toJSON === 'function') {
      value = value.toJSON(key);
    }

    if (typeof rep === 'function') {
      value = rep.call(holder, key, value);
    }

    switch (typeof value) {
      case 'string':
        return quote(value);

      case 'number':
        return isFinite(value) ? String(value) : 'null';

      case 'boolean':
      case 'null':
        return String(value);

      case 'object':
        if (!value) {
          return 'null';
        }

        gap += indent;
        partial = [];

        if (Object.prototype.toString.apply(value) === '[object Array]') {
          length = value.length;
          for (i = 0; i < length; i += 1) {
            partial[i] = str(i, value) || 'null';
          }

          v = partial.length === 0 ? '[]' : gap ? '[\n' + gap + partial.join(',\n' + gap) + '\n' + mind + ']' : '[' + partial.join(',') + ']';
          gap = mind;
          return v;
        }

        if (rep && typeof rep === 'object') {
          length = rep.length;
          for (i = 0; i < length; i += 1) {
            k = rep[i];
            if (typeof k === 'string') {
              v = str(k, value);
              if (v) {
                partial.push(quote(k) + (gap ? ': ' : ':') + v);
              }
            }
          }
        } else {
          for (k in value) {
            if (Object.hasOwnProperty.call(value, k)) {
              v = str(k, value);
              if (v) {
                partial.push(quote(k) + (gap ? ': ' : ':') + v);
              }
            }
          }
        }

        v = partial.length === 0 ? '{}' : gap ? '{\n' + gap + partial.join(',\n' + gap) + '\n' + mind + '}' : '{' + partial.join(',') + '}';
        gap = mind;
        return v;
    }
  }

  function stringify(value, replacer, space) {
    var i;
    gap = '';
    indent = '';
    if (typeof space === 'number') {
      for (i = 0; i < space; i += 1) {
        indent += ' ';
      }
    } else if (typeof space === 'string') {
      indent = space;
    }
    rep = replacer;
    if (replacer && typeof replacer !== 'function' && (typeof replacer !== 'object' || typeof replacer.length !== 'number')) {
      throw new Error('JSON.stringify');
    }
    return str('', { '': value });
  }

});


