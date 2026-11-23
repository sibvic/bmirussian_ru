logo = "sever-45.png"; 
logo_x = 425; 
logo_y = 120; 

tolerance = 0.2;
left_keychain_hole_size = 5;
right_keychain_hole_size = 0;
keychain_hole_size = 5;
size_x = 70; 
size_y = 23;
size_z = 3;
x_gap = 3;
y_gap = 3;

function GetLeftGap() = left_keychain_hole_size > 0 ? left_keychain_hole_size + x_gap * 2 : x_gap;
function GetRightGap() = right_keychain_hole_size > 0 ? right_keychain_hole_size + x_gap * 2 : x_gap;

cube_size_x = size_x - size_y;
left_gap = GetLeftGap();
right_gap = GetRightGap();
image_size_x = size_x - left_gap - right_gap;
image_size_y = image_size_x / logo_x * logo_y;

difference()
{
    union()
    {
        cube([cube_size_x, size_y, size_z]);
        translate([0, size_y / 2, 0]) 
            cylinder(h = size_z, r = size_y / 2);
        translate([cube_size_x, size_y / 2, 0]) 
            cylinder(h = size_z, r = size_y / 2);
    }
    translate([left_gap - size_y / 2 , (size_y - image_size_y) / 2 + y_gap, size_z]) 
        scale([image_size_x / logo_x, (image_size_y - y_gap * 2) / logo_y, size_z / 100]) 
        surface(file = logo, invert = true); 
    if (left_keychain_hole_size > 0)
    {
        translate([0 - size_y / 2 + 3 + left_keychain_hole_size / 2, size_y / 2, 0])
            cylinder(h = size_z, r = (left_keychain_hole_size + tolerance) / 2);
    }
    if (right_keychain_hole_size > 0)
    {
        translate([cube_size_x + size_y / 2 - 3 - right_keychain_hole_size / 2, size_y / 2, 0]) 
            cylinder(h = size_z, r = (right_keychain_hole_size + tolerance) / 2);
    }
}
