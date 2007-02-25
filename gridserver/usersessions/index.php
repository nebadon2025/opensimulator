<?
error_reporting(E_ALL); // Remember kids, PHP errors kill XML-RPC responses and REST too! will the slaughter ever end?

include("../gridserver_config.inc.php");
include("../../common/database.inc.php");

// Parse out the parameters from the URL
$params = str_replace($grid_home,'', $_SERVER['REQUEST_URI']);
$params = str_replace("index.php/","",$params);
$params = split('/',$params);

// Die if the key doesn't match
if($params[1]!=$sim_recvkey) {
//    die();
}

print_r($params);
// Send requested data
switch($params[0]) {
    case 'getasset':
	if($params[3]=="data") {
		Header("Content-Length: ". (string)filesize($asset_repos . "/" . $params[2] . "/data"));
		readfile($asset_repos . "/" . $params[2] . "/data");
	}
    break;
}
?>
