# Format the project using dotnet-format (target the .csproj file)
echo "Formatting project ../VulkanMC/VulkanMC/VulkanMC.csproj"
dotnet format ../VulkanMC/VulkanMC/VulkanMC.csproj

# Custom rule: Remove text between [] only in Logger lines, ignore obj/bin/*.csproj
find ../VulkanMC -name '*.cs' -not -path '../VulkanMC/obj/*' -not -path '../VulkanMC/bin/*' -not -name '*.csproj' | while read file; do
    count=$(grep -c 'Logger' "$file")
    while IFS= read -r line; do
        if [[ $line == *Logger* && $line == *'['* ]]; then
            before="$line"
            after=$(echo "$line" | sed 's/\[[^]]*\]//g')
            if [[ "$before" != "$after" ]]; then
                echo "Before: $before"
                echo "After : $after"
            fi
        fi
    done < "$file"
    sed -i '/Logger/ s/\[[^]]*\]//g' "$file"
    echo "Custom log rule applied to $file ($count Logger lines)"
done

echo "Formatting complete. Custom log rule applied."